
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Timers;
using System.Threading;

namespace DICOM_Manager
{
    public class dcmManager
    {
        public static List<string> attributeTagsToGet = new List<string>();
        private static Mutex mut = new Mutex();
        private static bool updateRunning;
        private static bool backupRunning;
        private static System.Timers.Timer uTimer = new System.Timers.Timer(600000);
        private static System.Timers.Timer bTimer = new System.Timers.Timer(600000);
        private static List<FSWatcher> FSWatchers = new List<FSWatcher>();
        public static bool testingmodeNoDelete = true;

        [STAThread]
        public static int Main(string[] args)
        {
            string tagInfoPath = "TagsToFind.txt";            
            List<string> tagsToGet = new List<string>();
            //string nDirPath = Path.GetDirectoryName(Application.ExecutablePath);
            //string uDirPath = Path.GetDirectoryName(Application.ExecutablePath);

            getIndexTagsFromFile(tagInfoPath, tagsToGet);
            dcmManager.attributeTagsToGet = tagsToGet;
                        
            SQLiteDatabase.initializeDB();

            dcmManager.uTimer.Elapsed += UpdateOnTimedEvent;
            dcmManager.uTimer.Enabled = true;
            dcmManager.bTimer.Elapsed += BackupOnTimedEvent;
            dcmManager.bTimer.Enabled = true;

            generateWatchersFromList();

            while(!ConsoleCommand.showConsoleOptions());
                       
            //Console.ReadLine();

            return 0;
        }

        private class FSWatcher
        {
            private FileSystemWatcher watcher;
            private int watchID;

            public int WatchID
            {
                get { return watchID; }
            }

            public void newWatcher(string dir_path, int watchdirID)
            {
                watchID = watchdirID;
                watcher = new FileSystemWatcher(dir_path);
                watcher.IncludeSubdirectories = true;
                watcher.Changed += new FileSystemEventHandler((s, e) => watcherOnChangeScanFileOrFolder(s, e, watchID));
                watcher.Created += new FileSystemEventHandler((s, e) => watcherScanNewFileOrFolder(s, e, watchID));
                watcher.Deleted += new FileSystemEventHandler((s, e) => watcherOnDeleteScanFileOrFolder(s, e, watchID));
                watcher.Renamed += new RenamedEventHandler((s, e) => watcherOnRenameScanFileOrFolder(s, e, watchID));
                watcher.EnableRaisingEvents = true;
            }

            public void deleteWatcher()
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }

        private static void generateWatchersFromList()
        {
            var watchIDs = SQLiteDatabase.getWatchedInfo(SQLiteDatabase.RowCheckTypes.isEnabled);
            for (int i = 0; i < watchIDs.Count; i++)
            {
                createWatcher(watchIDs[i]);
            }
        }

        private static void createWatcher(SQLiteDatabase.WatchedDirInfo winfo)
        {
            FSWatcher watcher = new FSWatcher();
            watcher.newWatcher(winfo.DirectoryPath, winfo.DirectoryID);
            FSWatchers.Add(watcher);
        }

        private static void removeWatcher(int watchid)
        {
            for (int i=FSWatchers.Count - 1; i >= 0; i--)
            {
                if (FSWatchers[i].WatchID == watchid) 
                {
                    FSWatchers[i].deleteWatcher();
                    FSWatchers.RemoveAt(i);
                }
            }
        }

        public static void watcherOnChangeScanFileOrFolder(object Source, FileSystemEventArgs e, int watchid)
        {
            var fileOrDirectory = enumDICOMFiles.checkIfFileOrDirectory(e.FullPath);//true if file, false if directory

            if(fileOrDirectory)
            {
                if (Path.GetExtension(e.FullPath) == ".dcm")
                {
                    verifyFileStatus(e.FullPath, watchid: watchid.ToString());
                }
            }
            else
            {
                updateDirectory(attributeTagsToGet, e.FullPath, watchID: watchid.ToString());
            }
        }

        public static void watcherOnDeleteScanFileOrFolder(object Source, FileSystemEventArgs e, int watchid)
        {
            if (!Directory.Exists(e.FullPath) && !File.Exists(e.FullPath))
            {
                var filesInPath = new List<string>();
                SQLiteDatabase.getFilePaths(filesInPath, e.FullPath);//Returns all the files in the db that are the path or are in a subdirectory of the path

                foreach (string fOffline in filesInPath)
                {
                    SQLiteDatabase.setFileCurrentlyActive(fOffline, false);//Sets the files current status to 'FALSE' for currently_online in the DB
                }
            }
        }

        public static void watcherOnRenameScanFileOrFolder(object Source, RenamedEventArgs e, int watchid)
        {
            var fileOrDirectory = enumDICOMFiles.checkIfFileOrDirectory(e.FullPath);//true if file, false if directory

            if (fileOrDirectory)
            {
                if (Path.GetExtension(e.FullPath) == ".dcm")
                {                    
                    verifyFileStatus(e.OldFullPath, watchid: watchid.ToString());
                    verifyFileStatus(e.FullPath, watchid: watchid.ToString());
                }
            }
            else
            {
                updateDirectory(attributeTagsToGet, e.FullPath, watchID: watchid.ToString());
                updateDirectory(attributeTagsToGet, e.OldFullPath, watchID: watchid.ToString());
            }
        }

        public static void watcherScanNewFileOrFolder(object Source, FileSystemEventArgs e, int watchid)
        {            
            var fileOrDirectory = enumDICOMFiles.checkIfFileOrDirectory(e.FullPath);//true if file, false if directory

            if (fileOrDirectory)
            {
                if (Path.GetExtension(e.FullPath) == ".dcm")
                {
                    verifyFileStatus(e.FullPath, watchid: watchid.ToString());
                }
            }
            else
            {
                updateDirectory(attributeTagsToGet, e.FullPath, watchID: watchid.ToString());
            }
        }

        public static void enableWatchDirectory(int dirID)
        {
            if (SQLiteDatabase.checkWatchID(dirID, SQLiteDatabase.RowCheckTypes.isDisabled))
            {
                SQLiteDatabase.setWatchCurrentlyActive(dirID, true);
                updateDirectory(attributeTagsToGet, dirID);
                //enable SystemFileWatch if implemented
                var watchinfo = SQLiteDatabase.getWatchedInfo(SQLiteDatabase.RowCheckTypes.watchID, id: dirID);
                createWatcher(watchinfo[0]);
            }
        }

        public static void disableWatchDirectory(int dirID)
        {            
            if (SQLiteDatabase.checkWatchID(dirID, SQLiteDatabase.RowCheckTypes.isEnabled))
            {                
                SQLiteDatabase.setWatchCurrentlyActive(dirID, false);
                //disable SystemFileWatch if added
                removeWatcher(dirID);
            }
        }

        public static void enableBackupSet(int backupID)
        {
            if (SQLiteDatabase.checkBackupSetID(backupID, SQLiteDatabase.RowCheckTypes.isDisabled))
            {
                SQLiteDatabase.setBackupCurrentlyActive(backupID, true);
            }
        }

        public static void disableBackupSet(int backupID)
        {
            if (SQLiteDatabase.checkBackupSetID(backupID, SQLiteDatabase.RowCheckTypes.isEnabled))
            {
                SQLiteDatabase.setBackupCurrentlyActive(backupID, false);
            }
        }

        private static void UpdateOnTimedEvent(object source, ElapsedEventArgs e)
        {
            if(updateRunning != true)
            {
                uTimer.Stop();
                updateRunning = true;
                var watchIDs = SQLiteDatabase.getWatchedInfo(SQLiteDatabase.RowCheckTypes.isEnabled);
                for (int i = 0; i < watchIDs.Count; i++)
                {
                    updateDirectory(watchIDs[i].DirectoryID);
                }
                updateRunning = false;
                uTimer.Start();
            }
        }

        public static void updateDirectory(int id)
        {
            var tempHolder = SQLiteDatabase.getWatchedInfo(SQLiteDatabase.RowCheckTypes.watchID, id);
            updateDirectory(attributeTagsToGet, tempHolder[0].DirectoryPath.ToLower());
        }

        public static void updateDirectory(List<string> tagsToGet, int id)
        {
            var tempHolder = SQLiteDatabase.getWatchedInfo(SQLiteDatabase.RowCheckTypes.watchID, id);
            updateDirectory(tagsToGet, tempHolder[0].DirectoryPath.ToLower());
        }

        public static void updateDirectory(List<string> tagsToGet, string uDirPath, string watchID = null)
        {                
                SQLiteDatabase.diskDBToMem();
                var dcmFilePaths = enumDICOMFiles.findDICOMInDirAndSubdir(@uDirPath);
                string watchedIDNumber = SQLiteDatabase.getWatchedID(uDirPath);//If the passed path is one of the autowatched directories, return the ID for it, if not returns null41
                if (watchedIDNumber == null) { watchedIDNumber=watchID;}//If the path isn't an autowatch directory, use possible value from optional parameter (used for subdirectories of watched directories)
                var currentFilesInPath = new List<string>();
                string tempDirString = uDirPath;            
                if (tempDirString.LastIndexOf("\\") != tempDirString.Count() - 1) { tempDirString = uDirPath + "\\"; }
                SQLiteDatabase.getFilePaths(currentFilesInPath, uDirPath);//Gets all files in db for the directory and subdirectories
            
                var currentOffline = currentFilesInPath.Except(dcmFilePaths);//Files listed in the DB for the directory but not present in the current directory scan
                foreach (string fOffline in currentOffline)
                {
                    SQLiteDatabase.setFileCurrentlyActive(fOffline, false);//Sets the files current status to 'FALSE' for currently_online in the DB
                }

                foreach (string fpath in dcmFilePaths)
                {
                    FileInfo finfo = new FileInfo(fpath);
                    var fsize = finfo.Length;
                    if (SQLiteDatabase.checkDBFilePathAndSize(fpath, fsize)) //Checks if the files in the DB already
                    {
                        if (SQLiteDatabase.checkDBFilePathAndSize(fpath, fsize, SQLiteDatabase.RowCheckTypes.isDisabled)) //Optional parameter adds a check if the file is listed as not online
                        {
                            SQLiteDatabase.setFileCurrentlyActive(fpath, true); //If the file is currently listed as offline sets to 'TRUE'
                        }
                    }
                    else
                    {
                        scanNewFile(fpath, tagsToGet, fromWatched: watchedIDNumber);
                    }
                }
                SQLiteDatabase.memDBToDisk();            
        }

        public static void scanNewDirectory(List<string> tagsToGet, string dirPath)//not currently used
        {
            string watchedIDNumber = SQLiteDatabase.getWatchedID(dirPath);
            if (watchedIDNumber == null) 
            { 
                SQLiteDatabase.addWatchDirectory(dirPath);
                watchedIDNumber = SQLiteDatabase.getWatchedID(dirPath);
            }
            

            var dcmFilePaths = enumDICOMFiles.findDICOMInDirAndSubdir(@dirPath);

            foreach (string fpath in dcmFilePaths)
            {
                scanNewFile(fpath, tagsToGet, fromWatched: watchedIDNumber);
            }
        }

        //public static void scanNewFile(string fpath, List<string> tagsToGet, string fromWatched = null)//Needs changes, first IF should only check filepath, then branch to options like an updated or modifed file or a new copy or complete new DICOM
        //{
        //    FileInfo finfo = new FileInfo(fpath);
        //    var fileSize = finfo.Length;
        //    if (!SQLiteDatabase.checkDBFilePathAndSize(fpath, fileSize))//checks if a file matching the path and file size is already in the db
        //    {
        //        var hash = fileHash.getFileHash(fpath);
        //        if (!SQLiteDatabase.checkDBHashAndSize(hash, fileSize))//checks if a file with the same hash and size is listed in the db
        //        {
        //            var tagAttributesFromFile = DICOMTagReader.scanDICOM(fpath, tagsToGet);
        //            SQLiteDatabase.addRowToDICOM_file_attributes(tagAttributesFromFile, fpath, hash, fileSize, watchedID: fromWatched);
        //        }
        //        else
        //        {
        //            SQLiteDatabase.addFileLocation(fpath, fileSize, hash, watchedID: fromWatched);
        //        }
        //    }
        //}

        //Scans files on detection of new instance or change. First IF should only check filepath, then branch to options like an updated or modifed file or a new copy or complete new DICOM
        public static void scanNewFile(string fpath, List<string> tagsToGet, string fromWatched = null)
        {
            FileInfo finfo = new FileInfo(fpath);
            var fileSize = finfo.Length;
            string fullPath = finfo.FullName;
			var hash = fileHash.getFileHash(fullPath);
            if (!SQLiteDatabase.checkIfFilePathInDB(fullPath))//checks if a file matching the path is already in the db
            {                
                if (!SQLiteDatabase.checkDBHashAndSize(hash, fileSize))//checks if a file with the same hash and size is listed in the db
                {
                    var tagAttributesFromFile = DICOMTagReader.scanDICOM(fullPath, tagsToGet);
                    SQLiteDatabase.addRowToDICOM_file_attributes(tagAttributesFromFile, fullPath, hash, fileSize, watchedID: fromWatched);
                }
                else
                {
                    SQLiteDatabase.addFileLocation(fullPath, fileSize, hash, watchedID: fromWatched);
                }
            }
			else
			{				
				var LocInfo = SQLiteDatabase.getFileLocationInfo(addChecks: SQLiteDatabase.RowCheckTypes.filePath, filepath: fullPath);
				var AttrInfo = SQLiteDatabase.getAttributeInfo(addChecks: SQLiteDatabase.RowCheckTypes.attributeID, id: LocInfo[0].AttributeID);
				if(hash != AttrInfo[0].Hash)
				{
					if (!SQLiteDatabase.checkDBHashAndSize(hash, fileSize))
					{
						var tagAttributesFromFile = DICOMTagReader.scanDICOM(fullPath, tagsToGet);
						SQLiteDatabase.addRowToDICOM_file_attributes(tagAttributesFromFile, fullPath, hash, fileSize, watchedID: fromWatched, newFileLocation: false);
						//to do: 1-get the new row, 2-use compareAttr class method, 3-write the differences to DB table of file changes (parameters=change type,effectedfile1,effectedfile2,note)
						var NewAttrInfo = SQLiteDatabase.getAttributeInfo(addChecks: SQLiteDatabase.RowCheckTypes.hash, hash: hash);
						string fileChanges = NewAttrInfo[0].compareAttr(AttrInfo[0]);
						SQLiteDatabase.addToChangeLog("Modified", LocInfo[0].FilePath, null, "Old Hash: " + AttrInfo[0].Hash + " New Hash: " + hash + " No match for new hash and file size in DB, " + fileChanges);
					}
					else
					{
						//change file_loc table so attributeID column points to new hash
						var NewAttrInfo = SQLiteDatabase.getAttributeInfo(addChecks: SQLiteDatabase.RowCheckTypes.hash, hash: hash);
						SQLiteDatabase.setFileAttributeIDForLocation(LocInfo[0].FilePath, NewAttrInfo[0].AttributeSetID);
						//write to DB table that file was renamed to an already existing file path
                        SQLiteDatabase.addToChangeLog("Modified/Renamed", LocInfo[0].FilePath, null, "File at indexed location changed to a new attribute set that already is recorded, Old Attribute Set ID: " + AttrInfo[0].AttributeSetID + " New Attribute Set ID: " + NewAttrInfo[0].AttributeSetID);
					}
				}								
			}
        }

        //Reads the file at fpath and loads the DICOM tag IDs in it into the collection tagsToGet (passed as reference)
        public static void getIndexTagsFromFile(string fpath, List<string> tagsToGet)
        {
            string tempHolding = "";
            try
            {
                using (StreamReader sr = new StreamReader(fpath))
                {
                    while (sr.Peek() >= 0)
                    {
                        tempHolding = sr.ReadLine();
                        if (tempHolding.Length == 8 && tempHolding.Substring(0, 2) != "//")
                        {
                            tagsToGet.Add(tempHolding);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The reading failed: {0}", e.ToString());
            }
        }

        public static void buildUniqueDICOMSet(string backupDirectoryPath)
        {
            //Prototype to create a complete set of DICOM files that has one of each of the matching files for each DICOM_file_attribute record
            //These will be stored in a new location and exist w/o duplication.
            //The backup set will have a table that stores a ID number for the set, the directory the set is stored in,
            //the last time the set was updated, a currently_active field, and a timestamp when it was created.
            var backupID = SQLiteDatabase.addBackupSet(backupDirectoryPath.ToLower());
            updateBackupSet(backupID);
        }

        public static void updateBackupSet(int backupID)
        {
            if (mut.WaitOne(1000))
            {
                backupRunning = true;
                SQLiteDatabase.diskDBToMem();
                var backupInfo = SQLiteDatabase.getBackupSetInfo(SQLiteDatabase.RowCheckTypes.setID, id: backupID);
                var allAttributeIDs = SQLiteDatabase.getAllAttributeSetIDs(SQLiteDatabase.TableList.FileAttribute);
                var allBackupAttributeIDs = SQLiteDatabase.getAllAttributeSetIDs(SQLiteDatabase.TableList.BackupLookupTable, SQLiteDatabase.RowCheckTypes.setID, set_id: backupID);
                //var backupLookupInfo = SQLiteDatabase.getBackupLookupInfo(SQLiteDatabase.RowCheckTypes.setID, set_id: backupID);
                var notInBackup = allAttributeIDs.Except(allBackupAttributeIDs);
                //var attributesetInfo = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.timeDateBefore, dateadded: DateTime.UtcNow);
                int j = 1;
                foreach (int i in notInBackup)
                {
                    var fileInfo = new List<string>();
                    string backupPath = backupInfo[0].BackupDirectoryPath + "\\DICOM_Backup_Set_" + backupInfo[0].BackupSetID;
                    SQLiteDatabase.getFilesForAttribute(i, fileInfo, SQLiteDatabase.RowCheckTypes.isEnabled); //Passes filInfo by reference
                    try 
                    {
                        if (!enumDICOMFiles.checkDirExists(backupPath)) { Directory.CreateDirectory(backupPath); }
                    }
                    catch(Exception e) { Console.WriteLine(e.Message);}
                    if (fileInfo.Count > 0)
                    {
                        try
                        {
                            var newFileName = Path.Combine(backupPath, ((backupInfo[0].LastNumberAssignedToFile + j).ToString() + ".dcm"));
                            FileInfo fnameCheck = new FileInfo(newFileName);
                            while (fnameCheck.Exists) //Checks if a file with that name already exists, if it does advances j
                            {
                                j++;
                                newFileName = Path.Combine(backupPath, ((backupInfo[0].LastNumberAssignedToFile + j).ToString() + ".dcm"));
                                fnameCheck = new FileInfo(newFileName);
                            }
                            File.Copy(fileInfo[0], newFileName);
                            SQLiteDatabase.setBackupLookupTablePair(i, backupID);
                            SQLiteDatabase.incrementBackupFileCount(backupID);
                            SQLiteDatabase.setLastNumberAssigned(backupID, backupInfo[0].LastNumberAssignedToFile + j);
                        }
                        catch (Exception e) { Console.WriteLine(e.Message); }
                        j++; //Since the value for LastNumberAssignedToFile is retrieved before the loop and thus doesn't change, j is advanced when a file is successfully added to the backup to account for that
                    }
                }
                SQLiteDatabase.setBackupLastUpdate(backupID);
                SQLiteDatabase.memDBToDisk();
                backupRunning = false;
                mut.ReleaseMutex();
            }
        }

        //Not to use anymore, Use function scanNewFile().
        public static void verifyFileAttributes(string fpath, List<string> tagsToGet = null, string watchID = null)
        {
            if(tagsToGet==null){tagsToGet = attributeTagsToGet;}
            var flInfo = SQLiteDatabase.getFileLocationInfo(SQLiteDatabase.RowCheckTypes.filePath, filepath: fpath);
            List<SQLiteDatabase.AttributeInfo> faInfo;
            string hashFromDB = null;
            if (flInfo != null) 
            { 
                faInfo = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.attributeID, id: flInfo[0].AttributeID);
                hashFromDB = faInfo[0].Hash;
            }
            var hash = fileHash.getFileHash(fpath);
            if (hash != hashFromDB && hashFromDB != null)
            {
                FileInfo finfo = new FileInfo(fpath);
                var fileSize = finfo.Length;
                if (!SQLiteDatabase.checkDBHashAndSize(hash, fileSize))//checks if a file with the same hash and size is listed in the db
                {
                    var tagAttributesFromFile = DICOMTagReader.scanDICOM(fpath, tagsToGet);
                    SQLiteDatabase.addRowToDICOM_file_attributes(tagAttributesFromFile, fpath, hash, fileSize, watchedID: watchID);
                }
                else
                {
                    //SQLiteDatabase.removeFileLocation(fpath, flInfo[0].AttributeID);
                    //SQLiteDatabase.addFileLocation(fpath, fileSize, hash, watchedID: watchID);
                    var AttrInfo = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.hash, hash: hash);
                    SQLiteDatabase.setFileAttributeIDForLocation(fpath, AttrInfo[0].AttributeSetID);
                }
            }
        }

        //checks if a file path exists in the file system. If it does, runs function scanNewFile(). 
        //If not, checks the DB for the file path. If the file path is in the DB it is set to inactive.
        public static void verifyFileStatus(string filePath, string watchid = null)
        {
            try 
            { 
                FileInfo finfo = new FileInfo(filePath);
                if (!finfo.Exists)
                {
                    var filelocInfo = SQLiteDatabase.getFileLocationInfo(SQLiteDatabase.RowCheckTypes.filePath, filepath: finfo.FullName);
                    if (filelocInfo != null) { SQLiteDatabase.setFileCurrentlyActive(finfo.FullName, false); }
                }
                else { scanNewFile(finfo.FullName, attributeTagsToGet, fromWatched: watchid); }
            }
            catch(Exception e) { Console.WriteLine("File Status Check Exception: {0}", e.Message);}            
        }

        private static void BackupOnTimedEvent(object source, ElapsedEventArgs e)
        {
            if (backupRunning != true)
            {
                bTimer.Stop();
                var backupIDs = SQLiteDatabase.getBackupSetInfo(SQLiteDatabase.RowCheckTypes.isEnabled);
                for (int i = 0; i < backupIDs.Count; i++)
                {
                    updateBackupSet(backupIDs[i].BackupSetID);
                }
                bTimer.Start();
            }
        }

        public static int getCountOfRedundantCopiesPassedLimit(int redundancyLimit)
        {
            int fileCount = 0;
            var redundantAttributeList = SQLiteDatabase.getRedundantFileCopies(redundancyLimit);

            for (int i = 0; i < redundantAttributeList.Count; i++)
            {
                var redundantFilePaths = SQLiteDatabase.getFileLocationInfo(SQLiteDatabase.RowCheckTypes.attributeID, attributeid: redundantAttributeList[i]);
                fileCount = fileCount + (redundantFilePaths.Count - redundancyLimit);
            }
            return fileCount;
        }
    
        public static void removeRedundantCopies(int redundancyLimit, List<string> directories_to_overlook = null, bool handleSameWatchIDasOneFile = false)
	    {
            if (redundancyLimit < 1) { return;}
		    //get attribute_id's that have copies exceeding the redundancy limit
            var redundantAttributeList = SQLiteDatabase.getRedundantFileCopies(redundancyLimit);
		
		    //get order of preference of watched dir            
            var directoryPreferanceOrder = SQLiteDatabase.getWatchedOrderingInfo();
            if(directoryPreferanceOrder.Count > 0){directoryPreferanceOrder.Sort(SQLiteDatabase.WatchedOrderInfo.SortByOrderPosition);}
		
            //loop
            for (int i = 0; i < redundantAttributeList.Count; i++)
            {
            //    -get DICOM_file_location Info collection for an attribute_id in set
                var redundantFilePaths = SQLiteDatabase.getFileLocationInfo(SQLiteDatabase.RowCheckTypes.attributeID, attributeid: redundantAttributeList[i]);
            //    //-add filepaths for matching attribute_id into a temp collection
            //    -sort collection by watch_id(desc) then filepath length(ascn) (ones with same watch_id, but longer filepath will be less preferred, watch_id is desc for next step)
                redundantFilePaths.Sort(SQLiteDatabase.FileLocationInfo.SortByFilePathLength);
            //    -(loop)scan (from ending) list for filepaths from first preferred directory and remove first found (remove all for that watch_id if optional treat-as-one is selected), repeat x(where x is the redundancy limit) for next watch_id in preference
                int noRemoval = 0;
                
                for (int j = 0; j < redundancyLimit; j++) //removes filepaths from the deletion list (so they are spared) starting from the most preferred directory
                {
                    int pathToRemove = -1;
                    if (directoryPreferanceOrder.Count > 0) { pathToRemove = redundantFilePaths.FindIndex(x => x.WatchedDirectoryID == directoryPreferanceOrder[j].WatchID); }
                    if (pathToRemove != -1) { redundantFilePaths.RemoveAt(pathToRemove); }
                    else { noRemoval++; }
                    
                }
                // if noRemoval is > 0, remove the difference between it and redundancyLimit from the end of the filepath list
                if (noRemoval > 0)
                {
                    while ((redundancyLimit - noRemoval) < redundancyLimit)
                    {
                        redundantFilePaths.RemoveAt(redundantFilePaths.Count - 1);
                        noRemoval--;
                    }
                }
                //    -(loop)delete file(s) from the list, update index DICOM_file_locations by removing entry
                while (redundantFilePaths.Count > 0)
                {
                    SQLiteDatabase.removeFileLocation(redundantFilePaths[redundantFilePaths.Count - 1].FilePath, redundantFilePaths[redundantFilePaths.Count - 1].AttributeID);
                    if (testingmodeNoDelete) { Console.WriteLine("File removed at " + redundantFilePaths[redundantFilePaths.Count-1].FilePath.ToString());}
                    else 
                    {
                        try
                        {
                            File.Delete(redundantFilePaths[redundantFilePaths.Count - 1].FilePath);
                        }
                        catch (DirectoryNotFoundException dirNotFound) { Console.WriteLine(dirNotFound.Message); }
                    
                    }
                    redundantFilePaths.RemoveAt(redundantFilePaths.Count - 1);
                }                
            }
	    }
    }
}
