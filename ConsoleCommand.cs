using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DICOM_Manager
{
    class ConsoleCommand
    {
        public static bool showConsoleOptions()
        {

            Console.WriteLine(" Type the number of what you would like to do then hit enter");
            Console.WriteLine(" 1 - Manage Watched Directories\n 2 - DICOM File Search and Information\n 3 - Manage Backup Sets\n 4 - Redundant Copy Controls\n"
                + " 5 - Toggle Testing Mode\n 6 - Exit\n");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder)
            {
                case "1": while (!showWatchConsoleOptions()) ; return false;
                case "2": while (!showSearchConsoleOptions()) ; return false;
                case "3": while (!showBackupConsoleOptions()) ; return false;
                case "4": while (!showRedundancyConsoleOptions()) ; return false;
                case "5": consoleToggleTestingMode(); return false;
                case "6": break;
                default: Console.WriteLine("Input not recognized\n"); return false; ;
            }
            return true;
        }

        public static bool showWatchConsoleOptions()
        {
            Console.WriteLine(" Type the number of what you would like to do then hit enter");
            Console.WriteLine(" 1 - View current watched directory list and statuses\n 2 - Add a directory to the watch list\n 3 - Enable a directory from the watch list\n"
            + " 4 - Disable a directory from the watch list\n 5 - Run update on watched directory\n 6 - Increase the preference of a Watched Directory\n"
            + " 7 - Decrease the preference of a Watched Directory\n 8 - Delete a Watched Directory\n 9 - Return to previous menu");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder)
            {
                case "1": while (!viewWatchedDirectories()) ; return false;
                case "2": consoleAddWatchDirectory(); return false;
                case "3": while (!enableDirectoryWatch()) ; return false;
                case "4": while (!disableDirectoryWatch()) ; return false;
                case "5": consoleUpdateWatchDirectory(); return false;
                case "6": increaseOrderPositionOfWatch(); return false;
                case "7": decreaseOrderPositionOfWatch(); return false;
                case "8": deleteWatchedDirectory(); return false;
                case "9": break;
                default: Console.WriteLine("Input not recognized\n"); return false; ;
            }
            return true;
        }

        public static bool showSearchConsoleOptions()
        {
            Console.WriteLine(" Type the number of what you would like to do then hit enter");
            Console.WriteLine(" 1 - Find DICOM file by patient name  \n 2 - Find DICOM file by patient ID\n"
            + " 3 - Find DICOM file by date created \n 4 - Show count of indexed DICOM files\n"
            + " 5 - Parse individual DICOM file and show all tags \n 6 - Return to previous menu");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder)
            {
                case "1": while (!consoleDisplayByPName()) ; return false;
                case "2": while (!consoleDisplayByPID()) ; return false;
                case "3": while (!consoleDisplayByDateTaken()) ; return false;
                case "4": while (!consoleDisplayCountofIndexedFiles()) ; return false;
                case "5": while (!consoleSingleFileParse()) ; return false;
                case "6": break;
                default: Console.WriteLine("Input not recognized\n"); return false; ;
            }
            return true;
        }

        public static bool showBackupConsoleOptions()
        {
            Console.WriteLine(" Type the number of what you would like to do then hit enter");
            Console.WriteLine(" 1 - View current backup sets and statuses\n 2 - Create a new backup set\n 3 - Enable automatic backup from the list\n"
            + " 4 - Disable automatic backup from the list\n 5 - Run update on a backup set\n 6 - Return to previous menu\n");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder)
            {
                case "1": while (!viewBackupSets()) ; return false;
                case "2": consoleAddBackupSet(); return false;
                case "3": while (!enableBackupSet()) ; return false;
                case "4": while (!disableBackupSet()) ; return false;
                case "5": consoleUpdateBackupSet(); return false;
                case "6": break;
                default: Console.WriteLine("Input not recognized\n"); return false; ;
            }
            return true;
        }

        public static bool showRedundancyConsoleOptions()
        {
            string redundancyLimit = "not set";
            var redundancyLimitTemp = SQLiteDatabase.getMiscInt("RedundancyControlLimit");
            if (redundancyLimitTemp != null) { redundancyLimit = redundancyLimitTemp.ToString(); }

            Console.WriteLine(" Type the number of what you would like to do then hit enter");
            Console.WriteLine(" 1 - Display the number of redundant copies passed the current limit \n"
                + " 2 - Set the default redundancy limit (Current is " + redundancyLimit + ")\n"
                + " 3 - Delete excess copies of DICOM files that number above redundancy limit\n"
                + " 4 - Return to previous menu");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder)
            {
                case "1": while (!consoleDisplayNumberOfRedundantCopies()) ; return false;
                case "2": while (!consoleSetRedundancyLimit()) ; return false;
                case "3": while (!consoleDeleteExcessCopies()) ; return false;
                case "4": break;
                default: Console.WriteLine("Input not recognized\n"); return false; ;
            }
            return true;
        }

        public static bool consoleDisplayNumberOfRedundantCopies()
        {
            int redundancyLimit = 0;
            var redundancyLimitTemp = SQLiteDatabase.getMiscInt("RedundancyControlLimit");
            if (redundancyLimitTemp != null) { redundancyLimit = redundancyLimitTemp.Value; }
            else { Console.WriteLine("No redundancy currently set, please set one and try again\n"); return true; }

            Console.WriteLine("There are currently {0} copies of files that exceed the limit of {1}\n", dcmManager.getCountOfRedundantCopiesPassedLimit(redundancyLimit), redundancyLimit);

            return true;
        }

        public static bool consoleSetRedundancyLimit()
        {
            Console.WriteLine("The redundancy limit is the number of copies of the same file that\n"
            + " are allowed to exist. If the number of copies for a file exceed this limit they will\n"
            + " be removed when the Delete Redundant Copies function is run\n");
            Console.WriteLine("Enter a integer value for the redundancy limit (Can't be less than 1): ");
            var limitHolder = Console.ReadLine();
            int tempLimit;
            if (Int32.TryParse(limitHolder, out tempLimit))
            {
                SQLiteDatabase.setRedundancyLimit(tempLimit);
            }
            else { Console.WriteLine("Invalid input\n"); }

            Console.WriteLine();
            return true;
        }

        public static bool consoleDeleteExcessCopies()
        {
            var redundancyLimitTemp = SQLiteDatabase.getMiscInt("RedundancyControlLimit");
            if (redundancyLimitTemp != null)
            {
                if (redundancyLimitTemp.Value < 1) { Console.WriteLine("Invalid redundancy limit value of {0}, please set it to a different value and try again", redundancyLimitTemp.Value); return true; }
                else { dcmManager.removeRedundantCopies(redundancyLimitTemp.Value); }
            }
            else { Console.WriteLine("No redundancy limit currently set, please set one and try again"); return true; }

            return true;
        }

        public static bool viewWatchedDirectories()
        {
            var watchInfo = SQLiteDatabase.getWatchedInfo();
            var watchOrder = SQLiteDatabase.getWatchedOrderingInfo();
            List<int> orderList = new List<int>();
            for (int i = 0; i < watchInfo.Count; i++)
            {
                int temp = watchOrder.FindIndex(x => x.WatchID == watchInfo[i].DirectoryID);
                int temp2 = watchOrder[temp].WatchDirectoryPositionInOrder;
                orderList.Add(temp2);
            }
            printWatchDirInfo(watchInfo, orderList);
            return true;
        }

        public static bool increaseOrderPositionOfWatch()
        {
            Console.WriteLine("Enter the Watch ID that you want to raise the preference of then hit Enter\n");
            var idHolder = Console.ReadLine();
            int tempid;
            if (Int32.TryParse(idHolder, out tempid))
            {
                SQLiteDatabase.setWatchOrderOfPreference(tempid, true);
            }
            return true;
        }

        public static bool decreaseOrderPositionOfWatch()
        {
            Console.WriteLine("Enter the Watch ID that you want to lower the preference of then hit Enter\n");
            var idHolder = Console.ReadLine();
            int tempid;
            if (Int32.TryParse(idHolder, out tempid))
            {
                SQLiteDatabase.setWatchOrderOfPreference(tempid, false);
            }
            return true;
        }

        public static bool viewBackupSets()
        {
            var backupInfo = SQLiteDatabase.getBackupSetInfo();
            printBackupSetInfo(backupInfo);
            return true;
        }

        public static bool enableDirectoryWatch()
        {
            Console.WriteLine("Enabling a directory watch will initiate a scan and index of DICOM files in the directory." +
                    " This could lead to a slow system response until the scan completes.\nType 'Y' if you wish to continue or 'N' if you do not.");
            var choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder.ToLower())
            {
                case "y": consoleEnableWatchDirectory(); break;
                case "n": return true;
                default: Console.WriteLine("Input not recognized\n"); return false;
            }

            return true;
        }

        public static bool disableDirectoryWatch()
        {
            Console.WriteLine("Disabling a directory watch will stop the automatic indexing of DICOM files in the directory." +
                    " \nType 'Y' if you wish to continue or 'N' if you do not.");
            var choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder.ToLower())
            {
                case "y": consoleDisableWatchDirectory(); break;
                case "n": return true;
                default: Console.WriteLine("Input not recognized\n"); return false;
            }
            return true;
        }

        public static bool enableBackupSet()
        {
            Console.WriteLine("Enabling automatic backups will have the system copy DICOM files that have been indexed into the backup directory at set intervals." +
                    " During such backups this could lead to slower system performance.\nType 'Y' if you wish to continue or 'N' if you do not.");
            var choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder.ToLower())
            {
                case "y": consoleEnableBackup(); break;
                case "n": return true;
                default: Console.WriteLine("Input not recognized\n"); return false;
            }

            return true;
        }

        public static bool disableBackupSet()
        {
            Console.WriteLine("Disabling automatic backup of a backup set will stop the automatic copying of DICOM files into the backup directory." +
                    " \nType 'Y' if you wish to continue or 'N' if you do not.");
            var choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder.ToLower())
            {
                case "y": consoleDisableBackup(); break;
                case "n": return true;
                default: Console.WriteLine("Input not recognized\n"); return false;
            }
            return true;
        }

        public static bool consoleDisplayByPName()
        {
            Console.WriteLine("Enter the name of the patient that you wish to locate. (Lastname Firstname)\n");
            Console.Write("Patienet name: ");
            var nameHolder = Console.ReadLine();
            string DICOMname = "";

            if (nameHolder.Length > 0)
            {
                if (nameHolder.IndexOf(' ') == 0) { nameHolder = nameHolder.Remove(0, 1); } //Remove any redundant spaces on the strings ends
                if (nameHolder.LastIndexOf(' ') == (nameHolder.Length - 1)) { nameHolder = nameHolder.Remove(nameHolder.Length - 1, 1); }
                DICOMname = nameHolder.Replace(' ', '^'); //In DICOM format name is written ln^fn^mn^prefix^postfix
            }
            //for (int i = 0; i < tempname.Count(); i++)
            //{
            //    if (tempname[i] != "" && i != tempname.Count()-1)
            //    {
            //        DICOMname += tempname[i] + "^";//might need changes to correct name input
            //    }
            //    if (tempname[i] != "" && i == tempname.Count() - 1)
            //    {
            //        DICOMname += tempname[i];
            //    }
            //}
            var pRecords = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.patientName, name: DICOMname);//look up matching DICOMs for the name, similar to the WatchedInfo function

            if (pRecords.Count != 0)
            {
                printAttributeInfo(pRecords);
            }
            else { Console.WriteLine("No matching records found\n"); }
            return true;
        }

        public static bool consoleDisplayByPID()
        {
            Console.WriteLine("Enter the patient ID of the patient that you wish to locate.");
            var idHolder = Console.ReadLine();
            //int tempid;
            //if (Int32.TryParse(idHolder, out tempid))
            //{
            //    var pRecords = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.patientID, pid: tempid);//look up matching DICOMs for the name, similar to the WatchedInfo function
            //    if (pRecords.Count != 0)
            //    {
            //        printAttributeInfo(pRecords);
            //    }
            //    else { Console.WriteLine("No matching records found\n"); }
            //}
            //else { Console.WriteLine("Invalid input\n");}

            if (idHolder.Length > 0)
            {
                var pRecords = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.patientID, pid: idHolder);//look up matching DICOMs for the name, similar to the WatchedInfo function
                if (pRecords.Count != 0)
                {
                    printAttributeInfo(pRecords);
                }
                else { Console.WriteLine("No matching records found\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }

            return true;
        }

        public static bool consoleDisplayByDateTaken()
        {
            Console.WriteLine("Enter the date of the study that you wish to locate, if the form of 'YYYY/MM/DD'.");
            var dateHolder = Console.ReadLine();
            DateTime tempdate = new DateTime();

            if (DateTime.TryParse(dateHolder, out tempdate))
            {
                var pRecords = SQLiteDatabase.getAttributeInfo(SQLiteDatabase.RowCheckTypes.studyDate, datetaken: tempdate.ToString("yyyy-MM-dd"));//look up matching DICOMs for the name, similar to the WatchedInfo function
                if (pRecords.Count != 0)
                {
                    printAttributeInfo(pRecords);
                }
                else { Console.WriteLine("No matching records found\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }

            return true;
        }

        public static bool consoleDisplayCountofIndexedFiles()
        {
            Console.WriteLine("There are {0} unique files indexed, with {1} total copies", SQLiteDatabase.getRowCountForTable(SQLiteDatabase.TableList.FileAttribute), SQLiteDatabase.getRowCountForTable(SQLiteDatabase.TableList.FileLocation));

            return true;
        }

        public static bool consoleSingleFileParse()
        {
            OpenFileDialog openFileDialog1 = new System.Windows.Forms.OpenFileDialog(); ;
            DialogResult result = openFileDialog1.ShowDialog();
            string openFileName = "";

            // OK button was pressed.
            if (result == DialogResult.OK)
            {
                openFileName = openFileDialog1.FileName;
                try
                {
                    DICOMTagReader.scanDICOM(openFileName, null, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred while attempting to load the file. The error is:\n"
                                    + e.ToString());
                }
                Console.WriteLine();
            }

            return true;
        }

        public static void printAttributeInfo(List<SQLiteDatabase.AttributeInfo> aInfo)
        {
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine("{0} records found for the search", aInfo.Count);
            Console.WriteLine("---------------------------------------------------------");
            for (int i = 0; i < aInfo.Count; i++)
            {
                Console.WriteLine("File ID:{0} Patient Name: {1} Patient ID: {2}", aInfo[i].AttributeSetID, aInfo[i].PatientFullName, aInfo[i].PatientID);
                Console.WriteLine("Date/Time Taken: " + aInfo[i].DateTaken.getDateString() + " " + aInfo[i].TimeTaken.getTimeString() + " Study Description: {0} \nIndexed On: {1}", aInfo[i].SeriesDescription, aInfo[i].Time_Date_Added);
                Console.WriteLine("---------------------------------------------------------");
            }
        }

        public static void printWatchDirInfo(List<SQLiteDatabase.WatchedDirInfo> wInfo, List<int> preferenceOrder)
        {
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine("{0} directories currently on the watch list", wInfo.Count);
            Console.WriteLine("---------------------------------------------------------");
            for (int i = 0; i < wInfo.Count; i++)
            {
                string tempStatus;

                if (wInfo[i].Status == "TRUE") { tempStatus = "Enabled"; }
                else { tempStatus = "Disabled"; }

                Console.WriteLine("Watched Directory ID:{0,-3:D3} Added On:{1,-20:g} \nWatch Status:{2,-12}", wInfo[i].DirectoryID, wInfo[i].TimeDateAdded, tempStatus);
                Console.WriteLine("Preference Order Position: " + preferenceOrder[i]);
                Console.WriteLine("File Path Watched:{0}", wInfo[i].DirectoryPath);
                Console.WriteLine("---------------------------------------------------------\n");
            }
        }

        public static void printBackupSetInfo(List<SQLiteDatabase.BackupSetInfo> bInfo)
        {
            Console.WriteLine("---------------------------------------------------------");
            Console.WriteLine("{0} backup sets currently found", bInfo.Count);
            Console.WriteLine("---------------------------------------------------------");
            for (int i = 0; i < bInfo.Count; i++)
            {
                string tempStatus;

                if (bInfo[i].Status == "TRUE") { tempStatus = "Enabled"; }
                else { tempStatus = "Disabled"; }

                Console.WriteLine("Backup Set ID:{0,-3:D3} Added On:{1,-20:g} \nAuto Backup Status:{2,-12} Last Updated On:{3,-20:g}", bInfo[i].BackupSetID, bInfo[i].TimeDateAdded, tempStatus, bInfo[i].LastUpdatedOn);
                Console.WriteLine("Backup Location:{0}", bInfo[i].BackupDirectoryPath);
                Console.WriteLine("---------------------------------------------------------\n");
            }
        }

        public static void consoleAddWatchDirectory()
        {
            string pathHolder = "";
            Console.WriteLine("WARNING\nNewly added directories are NOT set to be actively watched. To enable watching of this" +
                    " new directory go to 'Enable Watch Directory' under the 'Manage Watched Directories' menu.\n");
            //Console.WriteLine("Enter the full system path of the directory that you want to add. To cancel, enter a empty path.");


            // Show the FolderBrowserDialog.
            FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog(); ;
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                pathHolder = folderBrowserDialog1.SelectedPath;
            }


            //var pathHolder = Console.ReadLine();
            if (!enumDICOMFiles.checkDirExists(pathHolder))
            {
                Console.WriteLine("Invalid directory path\n");
            }
            else
            {
                SQLiteDatabase.addWatchDirectory(pathHolder);
                string watchedIDNumber = SQLiteDatabase.getWatchedID(pathHolder);
                Console.WriteLine("Directory added to watch list under ID:{0}\n", watchedIDNumber);
            }
        }

        public static void consoleAddBackupSet()
        {
            string pathHolder = "";
            Console.WriteLine("WARNING\nNewly added backup sets begin building immediatly. " +
                    "This might slow system responce until completed.\n");
            //Console.WriteLine("Enter the full system path of the directory that you want to add. To cancel, enter a empty path.");

            // Show the FolderBrowserDialog.
            FolderBrowserDialog folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog(); ;
            DialogResult result = folderBrowserDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                pathHolder = folderBrowserDialog1.SelectedPath;
            }

            //var pathHolder = Console.ReadLine();
            if (!enumDICOMFiles.checkDirExists(pathHolder))
            {
                Console.WriteLine("Invalid directory path\n");
            }
            else
            {
                dcmManager.buildUniqueDICOMSet(pathHolder);
                int backupIDNumber = SQLiteDatabase.getBackupSetInfo(SQLiteDatabase.RowCheckTypes.backupPath, dirpath: pathHolder)[0].BackupSetID;
                Console.WriteLine("Backup Created under ID:{0}\n", backupIDNumber);
            }
        }

        public static void consoleUpdateBackupSet()
        {
            Console.WriteLine("Enter the ID of the backup you wish to run an update for. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.getBackupSetInfo(SQLiteDatabase.RowCheckTypes.setID, id: idHolder).Count > 0)//Check to see if the ID is valid and inactive. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.updateBackupSet(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Backup Set ID\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void consoleEnableBackup()
        {
            Console.WriteLine("Enter the ID of the backup set you wish to enable automatic updating on. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkBackupSetID(idHolder, SQLiteDatabase.RowCheckTypes.isDisabled))//Check to see if the ID is valid and inactive. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.enableBackupSet(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Backup ID or is currently already enabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void consoleDisableBackup()
        {
            Console.WriteLine("Enter the ID of the backup you wish to disable automatic updating on. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkBackupSetID(idHolder, SQLiteDatabase.RowCheckTypes.isEnabled))//Check to see if the ID is valid and active. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.disableBackupSet(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Backup ID or is currently already disabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void consoleEnableWatchDirectory()
        {
            Console.WriteLine("Enter the ID of the directory you wish to enable a watch on. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkWatchID(idHolder, SQLiteDatabase.RowCheckTypes.isDisabled))//Check to see if the ID is valid and inactive. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.enableWatchDirectory(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Watch ID or is currently already enabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void consoleDisableWatchDirectory()
        {
            Console.WriteLine("Enter the ID of the directory you wish to disable watching on. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkWatchID(idHolder, SQLiteDatabase.RowCheckTypes.isEnabled))//Check to see if the ID is valid and active. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.disableWatchDirectory(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Watch ID or is currently already disabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void consoleUpdateWatchDirectory()
        {
            Console.WriteLine("Enter the ID of the directory you wish to update. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkWatchID(idHolder, SQLiteDatabase.RowCheckTypes.isEnabled))//Check to see if the ID is valid and active. Also checked in the next function as it is intended for more general use
                {
                    dcmManager.updateDirectory(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Watch ID or is currently disabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }
        }

        public static void deleteWatchedDirectory()
        {
            Console.WriteLine("WARNING: When a watch directory is deleted, all indexing of files in that directory is deleted as well. Note - This does not effect "
                + "the actual DICOM files, only the index that tracks them\n");
            Console.WriteLine("Enter the ID of the directory you wish to delete from the watch index. Enter a non-numeric character to cancel.");
            var inputHolder = Console.ReadLine();
            Console.WriteLine();
            int idHolder;

            if (Int32.TryParse(inputHolder, out idHolder))
            {
                if (SQLiteDatabase.checkWatchID(idHolder))//Check to see if the ID is valid.
                {
                    dcmManager.disableWatchDirectory(idHolder);
                    SQLiteDatabase.removeWatchDir(idHolder);
                }
                else { Console.WriteLine("Entered value is not a valid Watch ID or is currently already disabled\n"); }
            }
            else { Console.WriteLine("Invalid input\n"); }

        }

        public static void consoleToggleTestingMode()
        {
            Console.WriteLine("Testing mode is currently set to " + dcmManager.testingmodeNoDelete.ToString());
            Console.WriteLine("To change it type 'Y' and hit Enter, to cancel type anything else\n");
            string choiceHolder = Console.ReadLine();
            Console.WriteLine();

            switch (choiceHolder.ToLower())
            {
                case "y": dcmManager.testingmodeNoDelete = !dcmManager.testingmodeNoDelete; break;
                default: return;
            }
        }
    }
}
