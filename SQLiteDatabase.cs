using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using System.Data;
using System.IO;
using System.Threading;

namespace DICOM_Manager
{
    static class Extenstions
    {
        public static bool checkValidaton(this SQLiteDatabase.RowCheckTypes t, SQLiteDatabase.TableList l)
        {
            if (t == 0) { return false; }
            if (l == SQLiteDatabase.TableList.FileAttribute)
            {
                if ((int)t >= 10 && (int)t <= 19) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.timeDateBefore || t==SQLiteDatabase.RowCheckTypes.timeDateAfter) { return true; }
            }
            if (l == SQLiteDatabase.TableList.FileLocation)
            {
                if ((int)t >= 20 && (int)t <= 29) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.attributeID) { return true;}
                if (t == SQLiteDatabase.RowCheckTypes.isDisabled || t == SQLiteDatabase.RowCheckTypes.isEnabled) { return true; }
            }
            if (l == SQLiteDatabase.TableList.WatchList)
            {
                if ((int)t >= 30 && (int)t <= 39) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.isDisabled || t == SQLiteDatabase.RowCheckTypes.isEnabled) { return true; }
            }
            if (l == SQLiteDatabase.TableList.BackupSet)
            {
                if ((int)t >= 40 && (int)t <= 49) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.isDisabled || t == SQLiteDatabase.RowCheckTypes.isEnabled) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.timeDateBefore || t == SQLiteDatabase.RowCheckTypes.timeDateAfter) { return true; }
            }
            if (l == SQLiteDatabase.TableList.BackupLookupTable)
            {
                if ((int)t == 10 || (int)t == 40) { return true; }
                if (t == SQLiteDatabase.RowCheckTypes.timeDateBefore || t == SQLiteDatabase.RowCheckTypes.timeDateAfter) { return true; }
            }
            if (l == SQLiteDatabase.TableList.WatchOrder)
            {
                if ((int)t == 30) { return true; }
            }
            return false;
        }

        public static string getDateString(this DateTime? nullabledate)
        {
            if (nullabledate.HasValue)
            {
                DateTime correctedTime = nullabledate.Value;
                return correctedTime.ToString("yyyy/MM/dd");
            }
            else
            {
                return null;
            }
        }

        public static string getTimeString(this DateTime? nullabletime)
        {
            if (nullabletime.HasValue)
            {
                DateTime correctedTime = nullabletime.Value;
                return correctedTime.TimeOfDay.ToString();
            }
            else
            {
                return null;
            }
        }
    
        public static string getTableNameString(this SQLiteDatabase.TableList t)
        {
            string tableName;
            switch (t)
            {
                case SQLiteDatabase.TableList.FileAttribute: tableName = " DICOM_file_attributes "; break;
                case SQLiteDatabase.TableList.FileLocation: tableName = " DICOM_file_locations "; break;
                case SQLiteDatabase.TableList.WatchList: tableName = " DICOM_watch_directories "; break;
                case SQLiteDatabase.TableList.BackupSet: tableName = " DICOM_backup_sets "; break;
                case SQLiteDatabase.TableList.BackupLookupTable: tableName = " DICOM_backup_lookup "; break;
                case SQLiteDatabase.TableList.WatchOrder: tableName = " DICOM_watch_order "; break;
                case SQLiteDatabase.TableList.ChangeLog: tableName = " DICOM_changelog "; break;
                default: return null;
            }
            return tableName;
        }
    }

    class SQLiteDatabase
    {
        private static string dbPath = "ManagerDB.sqlite3";
        private static string defaultConnectionString;
        //private static SQLiteConnection memoryDB;//protected resource
        //private static ResourceProtector ProtectedResources = new ResourceProtector();
        //private static Mutex dbmut = new Mutex();
        private static Object globalDBLock = new Object();
        //private static bool inMemoryDBActive;//protected resource
        //private static DBSelection globalDBSelector = 0;//protected resource
        //private static int currentRunningQueries = 0;//protected resource
        //private static bool dbSourceChangeWaiting = false;//protected resource

        public class ResourceProtector
        {            
            //private static Mutex rpmutex = new Mutex();
            private static Object rpLock = new Object();
            private static bool inMemoryDBActive;//protected resource            
            private static DBSelection mDBSelector = 0;//protected resource
            private static int numCurrentRunningQueries = 0;//protected resource
            private static bool dbSourceChangeWaiting = false;//protected resource
            private static SQLiteConnection inmemoryDB;//protected resource
            private static int callsForInMemoery = 0;//protected resource

            public static SQLiteConnection InMemoryDBConnection
            {
                get { lock (rpLock) { return inmemoryDB; } }
                set { lock (rpLock) { inmemoryDB = value; } }
            }
            public static bool InMemoryDBActive
            {
                get { lock (rpLock) { return inMemoryDBActive; } }
                set { lock (rpLock) { inMemoryDBActive = value; } }
            }
            public static bool DBSourceChangeWaiting
            {
                get { lock (rpLock) { return dbSourceChangeWaiting; } }
                set { lock (rpLock) { dbSourceChangeWaiting = value;} }
            }           
            public static DBSelection MainDBSelector
            {
                get { lock (rpLock) { return mDBSelector; } }
                set { lock (rpLock) { mDBSelector = value; } }
            }
            public static int CurrentRunningQueries
            {
                get { lock (rpLock) { return numCurrentRunningQueries; } }
                set { lock (rpLock) { numCurrentRunningQueries = value; } }
            }
            public static int CallsForInMemoryDB
            {
                get { lock (rpLock) { return callsForInMemoery; } }
                set { lock (rpLock) { callsForInMemoery = value; } }
            }
        }

        public class WatchedDirInfo
        {
            private int dirID;
            private DateTime timeDateAdded;
            private string dirPath;
            private string status;
            
            public int DirectoryID
            {
                get { return dirID; }
                set { dirID = value; }
            }
            public DateTime TimeDateAdded
            {
                get { return timeDateAdded; }
                set { timeDateAdded = new DateTime(); timeDateAdded = value; }
            }
            public string DirectoryPath
            {
                get { return dirPath; }
                set { dirPath = value; }
            }
            public string Status
            {
                get { return status; }
                set { status = value; }
            }            
        }

        public class WatchedOrderInfo
        {
            private int watchID;
            private int watchOrderPosition;

            public int WatchID
            {
                get { return watchID; }
                set { watchID = value; }
            }
            public int WatchDirectoryPositionInOrder
            {
                get { return watchOrderPosition; }
                set { watchOrderPosition = value; }
            }

            public static Comparison<WatchedOrderInfo> SortByOrderPosition = delegate(WatchedOrderInfo ord1, WatchedOrderInfo ord2)
            {
                return ord1.WatchDirectoryPositionInOrder.CompareTo(ord2.WatchDirectoryPositionInOrder);
            };
        }

        public class pName
        {
            private string fname = null;
            private string mname = null;
            private string lname = null;
            private string nameprefix = null;
            private string namesuffix = null;

            public string FirstName
            {
                get { return fname; }
                set { fname = value; }
            }
            public string MiddleName
            {
                get { return mname; }
                set { mname = value; }
            }
            public string LastName
            {
                get { return lname; }
                set { lname = value; }
            }
            public string Prefix
            {
                get { return nameprefix; }
                set { nameprefix = value; }
            }
            public string Suffix
            {
                get { return namesuffix; }
                set { namesuffix = value; }
            }

            public void parseDICOMName(string name)
            {
                var temp = name.Split('^');
                if (temp.Count() != 0) 
                {
                    this.LastName = temp[0];
                    if (temp.Count() > 1) { this.FirstName = temp[1]; }
                    if (temp.Count() > 2) { this.MiddleName = temp[2]; }
                    if (temp.Count() > 3) { this.Prefix = temp[3]; }
                    if (temp.Count() > 4) { this.Suffix = temp[4]; }
                }
            }

            public override string ToString()
            {
                var fullname = this.Prefix + " " + this.FirstName + " " + this.MiddleName + " " + this.LastName + " " + this.Suffix;
                return fullname;
            }
        }

        public class AttributeInfo
        {
            private int tableid;
            private DateTime? datetaken;//00080012
            private DateTime? timetaken;//00080013
            private string seriesdescription;//0008103e
            private pName name = new pName();//00100010
            private string pid;//00100020
            private string studyid;//0020000d
            private string seriesid;//0020000e
            private string instanceid;//00080018
            private string fhash;
            private int fsize;
            private DateTime timedateadded;

            public int AttributeSetID
            {
                get { return tableid; }
                set { tableid = value; }
            }
            public DateTime? DateTaken
            {
                get { return datetaken; }
                set { datetaken = new DateTime(); datetaken = value; }
            }
            public DateTime? TimeTaken
            {
                get { return timetaken; }
                set { timetaken = new DateTime(); timetaken = value; }
            }
            public string SeriesDescription
            {
                get { return seriesdescription; }
                set { seriesdescription = value; }
            }
            public string PatientFirstName
            {
                get { return name.FirstName; }
                set { name.FirstName = value; }
            }
            public string PatientLastName
            {
                get { return name.LastName; }
                set { name.LastName = value; }
            }
            public string PatientID
            {
                get { return pid; }
                set { pid = value; }
            }
            public string StudyID
            {
                get { return studyid; }
                set { studyid = value; }
            }
            public string SeriesID
            {
                get { return seriesid; }
                set { seriesid = value; }
            }
            public string InstanceID
            {
                get { return instanceid; }
                set { instanceid = value; }
            }
            public string Hash
            {
                get { return fhash; }
                set { fhash = value; }
            }
            public int FileSize
            {
                get { return fsize; }
                set { fsize = value; }
            }
            public DateTime Time_Date_Added
            {
                get { return timedateadded; }
                set { timedateadded = new DateTime(); timedateadded = value; }
            }
            public string PatientFullName
            {
                get { return name.ToString(); }
                set { name.parseDICOMName(value); }
            }            

            public string compareAttr(AttributeInfo a)
            {
                string returnString = null;
                string differencesFound = "";
                int countDiff = 0;

                if (this.DateTaken != a.DateTaken) { differencesFound += "Differing on Date Taken "; countDiff++; }
                if (this.TimeTaken != a.TimeTaken) { differencesFound += "Differing on Time Taken "; countDiff++; }
                if (this.PatientFirstName != a.PatientFirstName) { differencesFound += "Differing on Patient First Name "; countDiff++; }
                if (this.PatientLastName != a.PatientLastName) { differencesFound += "Differing on Patient Last Name "; countDiff++; }
                if (this.PatientID != a.PatientID) { differencesFound += "Differing on Patient ID "; countDiff++; }
                if (this.StudyID != a.StudyID) { differencesFound += "Differing on Study ID "; countDiff++; }
                if (this.SeriesID != a.SeriesID) { differencesFound += "Differing on Series ID "; countDiff++; }
                if (this.InstanceID != a.InstanceID) { differencesFound += "Differing on Instance ID "; countDiff++; }
                if (this.SeriesDescription != a.SeriesDescription) { differencesFound += "Differing on Series Description "; countDiff++; }
                if (this.Hash != a.Hash) { differencesFound += "Differing on Hash "; countDiff++; }
                if (this.FileSize != a.FileSize) { differencesFound += "Differing on File Size "; countDiff++; }

                if (countDiff > 0)
                {
                    returnString = countDiff + " differences found: " + differencesFound;
                }
                return returnString; 
            }
        }

        public class FileLocationInfo
        {
            private int locationID;
            private int attributeID;
            private DateTime timeDateAdded;
            private int watchedFromID;
            private string filePath;
            private string status;

            public int LocationID
            {
                get { return locationID; }
                set { locationID = value; }
            }
            public int AttributeID
            {
                get { return attributeID; }
                set { attributeID = value; }
            }
            public DateTime TimeDateAdded
            {
                get { return timeDateAdded; }
                set { timeDateAdded = new DateTime(); timeDateAdded = value; }
            }
            public int WatchedDirectoryID
            {
                get {return watchedFromID;}
                set {watchedFromID = value;}
            }
            public string FilePath
            {
                get { return filePath; }
                set { filePath = value; }
            }
            public string Status
            {
                get { return status; }
                set { status = value; }
            }

            public static Comparison<FileLocationInfo> SortByAttributeID = delegate(FileLocationInfo loc1, FileLocationInfo loc2)
            {
                return loc1.AttributeID.CompareTo(loc2.AttributeID);
            };

            public static Comparison<FileLocationInfo> SortByWatchID = delegate(FileLocationInfo loc1, FileLocationInfo loc2)
            {
                return loc1.WatchedDirectoryID.CompareTo(loc2.WatchedDirectoryID);
            };

            public static Comparison<FileLocationInfo> SortByFilePathLength = delegate(FileLocationInfo loc1, FileLocationInfo loc2)
            {
                return loc1.FilePath.Length.CompareTo(loc2.FilePath.Length);
            };
        }

        public class BackupSetInfo
        {
            private int setID;
            private DateTime? lastUpdated;
            private DateTime timeDateAdded;
            private string backupPath;
            private string status;
            private int filecount;
            private int lastnumAssigned;

            public int BackupSetID
            {
                get { return setID; }
                set { setID = value; }
            }
            public DateTime? LastUpdatedOn
            {
                get { return lastUpdated;}
                set { lastUpdated = new DateTime(); lastUpdated = value; }
            }
            public DateTime TimeDateAdded
            {
                get { return timeDateAdded; }
                set { timeDateAdded = new DateTime(); timeDateAdded = value; }
            }
            public string BackupDirectoryPath
            {
                get { return backupPath; }
                set { backupPath = value; }
            }
            public string Status
            {
                get { return status; }
                set { status = value; }
            }
            public int FilesInBackup
            {
                get { return filecount;}
                set { filecount = value;}
            }
            public int LastNumberAssignedToFile
            {
                get { return lastnumAssigned;}
                set { lastnumAssigned = value;}
            }
        }

        public class BackupLookupInfo
        {
            private int setID;
            private int attributeID;
            private DateTime timeDateAdded;


            public int BackupSetID
            {
                get { return setID; }
                set { setID = value; }
            }
            public int AttributeSetID
            {
                get { return attributeID;}
                set { attributeID = value;}
            }
            public DateTime TimeDateAdded
            {
                get { return timeDateAdded; }
                set { timeDateAdded = new DateTime(); timeDateAdded = value; }
            }
        }
        //Types of checks that can be additionally added to a query. The value on them indicates the tables they work for, 1=DICOM_file_attribute 2=DICOM_file_locations 3=DICOM_watch_directories 4=DICOM_backup_sets 5=DICOM_backup_lookup
        //They can be used to check that a valid check has been specified for the query
        public enum RowCheckTypes
        {
            none = TableList.none,
            attributeID = 10,
            patientName = 11,
            patientID = 12,
            studyDate = 13,
            hash = 14,
            fileSize = 15,
            filePath = 20,
            watchedFromID = 21,
            watchID = 30,
            dirPath = 31,
            setID = 40,
            lastUpdate = 41,
            backupPath = 42,
            fileCount = 43,
            isEnabled,
            isDisabled,
            timeDateBefore,
            timeDateAfter
        };

        public enum TableList
        {
            none=0, FileAttribute=1, FileLocation=2, WatchList=3, BackupSet=4, BackupLookupTable=5, WatchOrder=6, ChangeLog=7, PresentInMultiple
        };

        public enum DBSelection
        {
            MainOnDisk=0, InMemory=1,
        };

        private static string getCheckString(RowCheckTypes t)
        {
            string checkString = "";
            switch (t)
            {
                case RowCheckTypes.watchID: checkString += " dir_id=@dID" ; break;
                case RowCheckTypes.dirPath: checkString += " directory_path=@dp "; break;
                case RowCheckTypes.filePath: checkString += " file_path=@fp "; break;
                case RowCheckTypes.watchedFromID: checkString += " watched_from_directory=@wid "; break;
                case RowCheckTypes.isEnabled: checkString += " currently_active='TRUE' "; break;
                case RowCheckTypes.isDisabled: checkString += " currently_active='FALSE' "; break;
                case RowCheckTypes.attributeID: checkString += " attribute_id=@id "; break;
                case RowCheckTypes.studyDate: checkString += " _00080012=@sd "; break;
                case RowCheckTypes.patientName: checkString += " _00100010 LIKE @pn "; break;
                case RowCheckTypes.patientID: checkString += " _00100020=@pid "; break;
                case RowCheckTypes.timeDateBefore: checkString += " time_date_added<=Datetime(@td) "; break;
                case RowCheckTypes.timeDateAfter: checkString += " time_date_added>=Datetime(@td) "; break;
                case RowCheckTypes.hash: checkString += " hash=@fh "; break;
                case RowCheckTypes.fileSize: checkString += " file_size=@fs "; break;
                case RowCheckTypes.setID: checkString += " set_id=@sid "; break;
                case RowCheckTypes.lastUpdate: checkString += " last_updated=@lu "; break;
                case RowCheckTypes.backupPath: checkString += " directory_path=@bp "; break;
                case RowCheckTypes.fileCount: checkString += " file_count=@fc "; break;
                default: return null;
            };
            return checkString;
        }

        //Used to initialize the DB in the directory where the application is
        public static void initializeDB()
        {
            initializeDB(null);
        }

        //Initializes the database and its tables if they are not present. Sets the dbPath variable to a nondefault location if passed.
        public static void initializeDB(string dbp)
        {
            if (dbp != null) { dbPath = dbp; }
            defaultConnectionString = "Data Source=" + dbPath + ";Version=3;";
                                    
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    mydbConnection.Open();
                }
                //end of protected area                

                using (SQLiteTransaction sqlTransaction = mydbConnection.BeginTransaction())
                {

                    string createTable = "CREATE TABLE IF NOT EXISTS DICOM_file_attributes (attribute_id INTEGER PRIMARY KEY AUTOINCREMENT, _00080012 DATE, _00080013 TIME, _0008103E TEXT," +
                        " _00100010 TEXT COLLATE NOCASE, _00100020 TEXT COLLATE NOCASE, _0020000D TEXT, _0020000E TEXT, _00080018 TEXT, hash TEXT COLLATE NOCASE, file_size BIGINT," +
                        " time_date_added TIMEDATE)";

                    using (SQLiteCommand command = new SQLiteCommand(createTable, mydbConnection))
                    {
                        command.ExecuteNonQuery();
                    }

                    string createTable2 = "CREATE TABLE IF NOT EXISTS DICOM_file_locations (location_id INTEGER PRIMARY KEY AUTOINCREMENT, attribute_id INT, time_date_added TIMEDATE, watched_from_directory INT, file_path TEXT COLLATE NOCASE," +
                        " currently_active BOOLEAN)";

                    using (SQLiteCommand command2 = new SQLiteCommand(createTable2, mydbConnection))
                    {
                        command2.ExecuteNonQuery();
                    }

                    string createTable3 = "CREATE TABLE IF NOT EXISTS DICOM_watch_directories (dir_id INTEGER PRIMARY KEY AUTOINCREMENT, time_date_added TIMEDATE, directory_path TEXT COLLATE NOCASE," +
                        " currently_active BOOLEAN)";

                    using (SQLiteCommand command3 = new SQLiteCommand(createTable3, mydbConnection))
                    {
                        command3.ExecuteNonQuery();
                    }

                    string createTable4 = "CREATE TABLE IF NOT EXISTS DICOM_backup_sets (set_id INTEGER PRIMARY KEY AUTOINCREMENT, last_updated TIMEDATE, directory_path TEXT COLLATE NOCASE," +
                        " currently_active BOOLEAN, time_date_added TIMEDATE, file_count INT, last_number_assigned INT)";

                    using (SQLiteCommand command4 = new SQLiteCommand(createTable4, mydbConnection))
                    {
                        command4.ExecuteNonQuery();
                    }

                    string createTable5 = "CREATE TABLE IF NOT EXISTS DICOM_backup_lookup (set_id INTEGER , attribute_id INTEGER," +
                        " time_date_added TIMEDATE)";

                    using (SQLiteCommand command5 = new SQLiteCommand(createTable5, mydbConnection))
                    {
                        command5.ExecuteNonQuery();
                    }

                    string createTable6 = "CREATE TABLE IF NOT EXISTS DICOM_changelog (change_type TEXT , effected_file_1 TEXT," +
                        " effected_file_2 TEXT, additional_note TEXT)";

                    using (SQLiteCommand command6 = new SQLiteCommand(createTable6, mydbConnection))
                    {
                        command6.ExecuteNonQuery();
                    }

                    string createTable7 = "CREATE TABLE IF NOT EXISTS DICOM_watch_order (order_position INTEGER PRIMARY KEY AUTOINCREMENT, watch_id INT)";

                    using (SQLiteCommand command7 = new SQLiteCommand(createTable7, mydbConnection))
                    {
                        command7.ExecuteNonQuery();
                    }

                    string createTable8 = "CREATE TABLE IF NOT EXISTS DICOM_misc_values (variable_name TEXT PRIMARY KEY, integer_value INT, "
                    + " float_value REAL, string_value TEXT)";

                    using (SQLiteCommand command8 = new SQLiteCommand(createTable8, mydbConnection))
                    {
                        command8.ExecuteNonQuery();
                    }

                    string createIndex1 = "CREATE INDEX IF NOT EXISTS `attribute_search_index` ON `DICOM_file_attributes` (`hash` ASC,`file_size` ASC);";
                    using (SQLiteCommand command4 = new SQLiteCommand(createIndex1, mydbConnection))
                    {
                        command4.ExecuteNonQuery();
                    }

                    string createIndex2 = "CREATE INDEX IF NOT EXISTS `attribute_file_size_index` ON `DICOM_file_attributes` (`file_size` ASC);";
                    using (SQLiteCommand command5 = new SQLiteCommand(createIndex2, mydbConnection))
                    {
                        command5.ExecuteNonQuery();
                    }

                    string createIndex3 = "CREATE INDEX IF NOT EXISTS `file_loc_index` ON `DICOM_file_locations` (`file_path` ASC);";
                    using (SQLiteCommand command6 = new SQLiteCommand(createIndex3, mydbConnection))
                    {
                        command6.ExecuteNonQuery();
                    }

                    sqlTransaction.Commit();
                }
            }
            ResourceProtector.CurrentRunningQueries--;
        }

        //Copies and switches the current default database from residing on disk to in-memory
        public static void diskDBToMem()
        {
            ResourceProtector.CallsForInMemoryDB++;
            ResourceProtector.DBSourceChangeWaiting = true;
            bool waitCheck = true;
            bool lockWasTaken = false;

            try
            {
                while (waitCheck)
                {
                    try
                    {
                        Monitor.Enter(globalDBLock, ref lockWasTaken);
                        if (ResourceProtector.CurrentRunningQueries == 0) { waitCheck = false; }                        
                    }
                    finally
                    {
                        if (lockWasTaken && waitCheck) 
                        { 
                            Monitor.Exit(globalDBLock);
                            lockWasTaken = false;
                        }
                    }
                }

                if (!ResourceProtector.InMemoryDBActive)
                {
                    ResourceProtector.InMemoryDBConnection = new SQLiteConnection("Data Source=:memory:;Version=3;");
                    ResourceProtector.InMemoryDBConnection.Open();
                    using (SQLiteConnection diskDB = new SQLiteConnection(defaultConnectionString))
                    {
                        diskDB.Open();

                        // copy db file to memory
                        diskDB.BackupDatabase(ResourceProtector.InMemoryDBConnection, "main", "main", -1, null, 0);
                        ResourceProtector.InMemoryDBActive = true;
                        ResourceProtector.MainDBSelector = DBSelection.InMemory;
                        ResourceProtector.DBSourceChangeWaiting = false;
                    }
                }
            }
            finally { if (lockWasTaken)Monitor.Exit(globalDBLock); }                
        }

        //Copies and switches the current default database from in-memory to on disk
        public static void memDBToDisk()
        {
            ResourceProtector.DBSourceChangeWaiting = true;
            bool waitCheck = true;
            bool lockWasTaken = false;

            try
            {
                while (waitCheck)
                {
                    try
                    {
                        Monitor.Enter(globalDBLock, ref lockWasTaken);
                        if (ResourceProtector.CurrentRunningQueries == 0) { waitCheck = false; }                        
                    }
                    finally
                    {
                        if (lockWasTaken && waitCheck) { Monitor.Exit(globalDBLock); }
                    }
                }
                
                if (ResourceProtector.InMemoryDBActive && ResourceProtector.CallsForInMemoryDB == 1)
                {
                    using (SQLiteConnection diskDB = new SQLiteConnection(defaultConnectionString))
                    {
                        diskDB.Open();

                        // save memory db to file
                        ResourceProtector.InMemoryDBConnection.BackupDatabase(diskDB, "main", "main", -1, null, 0);
                        diskDB.Close();
                        ResourceProtector.InMemoryDBConnection.Close();
                    }
                    ResourceProtector.InMemoryDBActive = false;
                    ResourceProtector.MainDBSelector = DBSelection.MainOnDisk;
                    ResourceProtector.DBSourceChangeWaiting = false;
                }
                ResourceProtector.CallsForInMemoryDB--;
            }
            finally { if (lockWasTaken)Monitor.Exit(globalDBLock); }                
        }        

        //Checks if the database holds a reference to the file path fpath and that the file referenced also was of the same size (in bytes)
        //Used to determine if a file has already been indexed, makes no guarantee that the files inner information (tag attribute) haven't changed
        //To check for inner data changes use a comparison to stored file hash in DICOM_file_attribute.hash
        public static bool checkDBFilePathAndSize(string fpath, long fs, RowCheckTypes addCheck = 0, DBSelection? db = null)
        {
            bool fileExists;
            string fp = fpath;
            string optionalCheckString = null;            

            if (addCheck == RowCheckTypes.isDisabled) { optionalCheckString = " AND fl.currently_active = 'FALSE' "; }
            if (addCheck == RowCheckTypes.isEnabled) { optionalCheckString = " AND fl.currently_active = 'TRUE' "; }
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while(ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25);}

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT fa.attribute_id From DICOM_file_attributes AS fa INNER JOIN DICOM_file_locations AS fl ON fa.attribute_id=fl.attribute_id" +
                    " WHERE fl.file_path = @fp AND fa.file_size = @fs " + optionalCheckString;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@fp", fp);
                    command.Parameters.AddWithValue("@fs", fs);
                    var reader = command.ExecuteReader();
                    fileExists = reader.Read();
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;                
            }            
            return fileExists;
        }

        //Adds a row to the DICOM_file_attributes table. As no previous reference in that table means that there is no related entry in DICOM_file_locations
        //this function also adds the the related entry into DICOM_file_location.
        public static void addRowToDICOM_file_attributes(DICOMTagReader.TagValuePair fAttributes, string fpath, string hash, long fsize, string watchedID = null, bool newFileLocation = true,  DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string tagIDs = "";
                string parameters = "";
                int i = 0;                
                
                //Builds the tagIDs and parameters strings that are used to select the rows to add to and then adds the related values as parameters
                foreach (string e in fAttributes.TagID)
                {
                    
                    tagIDs += "_" + e + ", ";
                    parameters += "@a" + i + ", ";
                    i++;
                }

                string insertString = "INSERT INTO DICOM_file_attributes (" + tagIDs + "hash, file_size, time_date_added) " +
                    "VALUES (" + parameters + "@fh, @fs, datetime('now'))";

                
                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    for (int j = 0; j < i; j++)
                    {   //Spot where it actually adds the parameters from the above string builder function
                        command.Parameters.AddWithValue(("@a" + j), fAttributes.Value[j]);
                    }
                    command.Parameters.AddWithValue("@fh", hash);
                    command.Parameters.AddWithValue("@fs", fsize);
                    command.ExecuteNonQuery();
                }


                if (newFileLocation == true)
                {
                    if (watchedID != null)
                    {
                        addFileLocation(fpath, fsize, hash, watchedID, db: db);
                    }
                    else
                    {
                        addFileLocation(fpath, fsize, hash, db: db);
                    }
                }

                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Adds a row to the table DICOM_file_location. Used when a file with a new set of attribute tags is located of a file on a new path is located that maps to an existing attribute record
        public static void addFileLocation(string fpath, long fsize, string hash, string watchedID = null, DBSelection? db = null)
        {
            int idHolder = 1000000;
            string optionalSetting = null;
            string optionalValue = null;

            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string getAttributeTableID = "SELECT attribute_id FROM DICOM_file_attributes WHERE hash='" + hash + "' AND file_size='" + fsize + "'";

                using (SQLiteCommand getID = new SQLiteCommand(getAttributeTableID, dbSelectConnection))
                {
                    var reader = getID.ExecuteReader();
                    if (reader.Read()) { idHolder = reader.GetInt32(0); }
                    reader.Close();
                }

                if (watchedID != null) 
                { 
                    optionalSetting = ", watched_from_directory";
                    optionalValue = ", @wl";
                }

                string insertString2 = "INSERT INTO DICOM_file_locations (attribute_id, time_date_added, file_path, currently_active" + optionalSetting +
                    ") VALUES (@id, datetime('now'), @fp, 'TRUE'" + optionalValue + ")";

                
                using (SQLiteCommand command2 = new SQLiteCommand(insertString2, dbSelectConnection))
                {
                    command2.Parameters.AddWithValue("@id", idHolder);
                    command2.Parameters.AddWithValue("@fp", fpath.ToLower());
                    if (watchedID != null) { command2.Parameters.AddWithValue("@wl", watchedID); }
                    command2.ExecuteNonQuery();
                }

                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Removes a row from the DICOM_file_location table. Use only when a delete is detected by FSW, otherwise should use function 
        //to set row value for currently_active to 'FALSE'.
        public static void removeFileLocation(string fpath, int attributeID, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "DELETE FROM DICOM_file_locations WHERE attribute_id=@id AND file_path=@fp";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@fp", fpath);
                    command.Parameters.AddWithValue("@id", attributeID);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Checks the DICOM_file_attribute table if the hash and file size already exist. 
        public static bool checkDBHashAndSize(string hash, long fsize, DBSelection? db = null)
        {
            bool attributeRecordExists;
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT attribute_id From DICOM_file_attributes AS fl" +
                    " WHERE fl.hash = @fhash AND fl.file_size = @fsize";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@fhash", hash);
                    command.Parameters.AddWithValue("@fsize", fsize);
                    var reader = command.ExecuteReader();
                    attributeRecordExists = reader.Read();
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return attributeRecordExists;
        }

        //Returns a collection of the full file paths from all file that were contained in and in subdirectories of the passed searchDir
        public static void getFilePaths(List<string> pathList, string searchDir, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                searchDir += "%";

                string searchString = "SELECT file_path From DICOM_file_locations" +
                    " WHERE file_path LIKE @sDir";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@sDir", searchDir);                    
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        pathList.Add(reader.GetString(0).ToLower());
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Changes the state of currently_active in the DICOM_file_locations table to a passed value
        public static void setFileCurrentlyActive(string @fpath, bool setTo, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string setToValue;
                if (setTo == true) { setToValue = "TRUE"; }
                else { setToValue = "FALSE"; }

                string searchString = "SELECT location_id From DICOM_file_locations" +
                    " WHERE file_path LIKE @fp";
                int locID = -1;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@fp", fpath + "%");
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        locID = reader.GetInt32(0);
                    }
                    reader.Close();
                }

                string updateString = "UPDATE DICOM_file_locations SET currently_active = @onlineVal WHERE location_id=@loc";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@onlineVal", setToValue);
                    command.Parameters.AddWithValue("@loc", locID);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Changes the value of attributeID in the DICOM_file_locations table to a passed value
        public static void setFileAttributeIDForLocation(string fpath, int setAttrIDTo, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area
                                
                string updateString = "UPDATE DICOM_file_locations SET attribute_id=@id WHERE file_path=@fp";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@id", setAttrIDTo);
                    command.Parameters.AddWithValue("@fp", fpath);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Changes the state of currently_active in the DICOM_watch_directories table to the passed value
        public static void setWatchCurrentlyActive(int id, bool setTo, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string setToValue;
                if (setTo == true) { setToValue = "TRUE"; }
                else { setToValue = "FALSE"; }

                string updateString = "UPDATE DICOM_watch_directories SET currently_active = @activeVal WHERE dir_id=@dID";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@activeVal", setToValue);
                    command.Parameters.AddWithValue("@dID", id);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Increases or decreases order of preference of the watchID
        public static void setWatchOrderOfPreference(int watchID, bool increaseOrDecreasePreferenceOrder, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string selectString = "SELECT order_position FROM DICOM_watch_order WHERE watch_id=@wid";
                int orderPosition = 999999999;
                bool didItRead = false;

                using (SQLiteCommand command1 = new SQLiteCommand(selectString, dbSelectConnection))
                {
                    command1.Parameters.AddWithValue("@wid", watchID);
                    var reader = command1.ExecuteReader();
                    if (reader.Read())
                    {
                        orderPosition = reader.GetInt32(0);
                        didItRead = true;
                    }
                    reader.Close();
                }

                if (!didItRead) { return; }

                string selectString2 = "SELECT watch_id FROM DICOM_watch_order WHERE order_position=@oop"; //finds the watch_id of the record that needs to exchange ordering, if none than current record is already the top and/or bottom of order
                int otherOrderPosition = orderPosition;                
                if (increaseOrDecreasePreferenceOrder) { otherOrderPosition--; }
                else { otherOrderPosition++; }

                int secondWatchID = 999999999;
                didItRead = false;

                using (SQLiteCommand command2 = new SQLiteCommand(selectString2, dbSelectConnection))
                {
                    command2.Parameters.AddWithValue("@oop", otherOrderPosition);
                    var reader = command2.ExecuteReader();
                    if (reader.Read())
                    {
                        secondWatchID = reader.GetInt32(0);
                        didItRead = true;
                    }
                    reader.Close();
                }

                if (!didItRead) { return;}

                using (SQLiteTransaction sqlTransaction = mydbConnection.BeginTransaction())
                {
                    int tempPosition = 999999999;
                    string updateString = "UPDATE DICOM_watch_order SET order_position = @orderVal WHERE watch_id=@wid";

                    using (SQLiteCommand command3 = new SQLiteCommand(updateString, dbSelectConnection))
                    {
                        command3.Parameters.AddWithValue("@orderVal", tempPosition);
                        command3.Parameters.AddWithValue("@wid", secondWatchID);
                        command3.ExecuteNonQuery();
                    }

                    string updateString2 = "UPDATE DICOM_watch_order SET order_position = @orderVal WHERE watch_id=@wid";

                    using (SQLiteCommand command4 = new SQLiteCommand(updateString2, dbSelectConnection))
                    {
                        command4.Parameters.AddWithValue("@orderVal", otherOrderPosition);
                        command4.Parameters.AddWithValue("@wid", watchID);
                        command4.ExecuteNonQuery();
                    }

                    string updateString3 = "UPDATE DICOM_watch_order SET order_position = @orderVal WHERE watch_id=@wid";

                    using (SQLiteCommand command5 = new SQLiteCommand(updateString3, dbSelectConnection))
                    {
                        command5.Parameters.AddWithValue("@orderVal", orderPosition);
                        command5.Parameters.AddWithValue("@wid", secondWatchID);
                        command5.ExecuteNonQuery();
                    }
                    sqlTransaction.Commit();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Changes the state of currently_active in the DICOM_backup_sets table to the passed value
        public static void setBackupCurrentlyActive(int id, bool setTo, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string setToValue;
                if (setTo == true) { setToValue = "TRUE"; }
                else { setToValue = "FALSE"; }

                string updateString = "UPDATE DICOM_backup_sets SET currently_active = @activeVal WHERE set_id=@sid";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@activeVal", setToValue);
                    command.Parameters.AddWithValue("@sid", id);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Adds a new row for a directory to the DICOM_watch_directories table
        public static void addWatchDirectory(string dirPath, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area
               
                string insertString = "INSERT INTO DICOM_watch_directories (time_date_added, directory_path, currently_active)"
                + "VALUES (datetime('now'), @dPath, 'FALSE')";

                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@dpath", dirPath.ToLower());
                    command.ExecuteNonQuery();
                }

                string watchedIDNumber = SQLiteDatabase.getWatchedID(dirPath.ToLower());

                string insertString2 = "INSERT INTO DICOM_watch_order (watch_id)"
                + "VALUES (@wid)";

                using (SQLiteCommand command2 = new SQLiteCommand(insertString2, dbSelectConnection))
                {
                    command2.Parameters.AddWithValue("@wid", watchedIDNumber);
                    command2.ExecuteNonQuery();
                }

                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Adds a new row to make a new backup set and directory, returns the set_id for the newly created row
        public static int addBackupSet(string backupPath, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string insertString = "INSERT INTO DICOM_backup_sets (time_date_added, directory_path, currently_active, file_count, last_number_assigned)"
                + "VALUES (datetime('now'), @dPath, 'TRUE', '0', '0')";

                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@dpath", backupPath.ToLower());
                    command.ExecuteNonQuery();
                }

                int idHolder = 9999999;
                string getBackupSetID = "SELECT set_id FROM DICOM_backup_sets WHERE directory_path=@dPath";

                using (SQLiteCommand getID = new SQLiteCommand(getBackupSetID, dbSelectConnection))
                {
                    getID.Parameters.AddWithValue("@dpath", backupPath.ToLower());
                    var reader = getID.ExecuteReader();
                    if (reader.Read()) { idHolder = reader.GetInt32(0); }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
                return idHolder;
            }
        }

        //Increments the value of file_count in DICOM_backup_sets for a certain set_id
        public static void incrementBackupFileCount(int id, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string updateString = "UPDATE DICOM_backup_sets SET file_count=file_count+1 WHERE set_id=@sid";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {                    
                    command.Parameters.AddWithValue("@sid", id);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Sets the date that the last time an update was run for a backup set, by default sets to the current utc time, can pass other date/time as an optional parameter
        public static void setBackupLastUpdate(int id, DateTime? setDateTo = null, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string dateFormat = null;

                if (setDateTo == null)
                {
                    dateFormat = DateTime.UtcNow.ToString("yyyy-MM-dd hh:mm:ss");
                }
                else
                {
                    dateFormat = setDateTo.Value.Date.ToString("yyyy-MM-dd hh:mm:ss");
                }

                string updateString = "UPDATE DICOM_backup_sets SET last_updated=@lu WHERE set_id=@sid";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@lu", dateFormat);
                    command.Parameters.AddWithValue("@sid", id);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        public static void setLastNumberAssigned(int setID, int lastAssigned, DBSelection? db= null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string updateString = "UPDATE DICOM_backup_sets SET last_number_assigned=@lna WHERE set_id=@sid";

                using (SQLiteCommand command = new SQLiteCommand(updateString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@lna", lastAssigned);
                    command.Parameters.AddWithValue("@sid", setID);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Returns a collection of objects each of which can hold the contents of one row of the table
        //By default returns all rows in table, can pass optional check parameter to return a subset instead
        //To do so, set the value of addChecks to the enum type that matches the table column you want to check against, then set the value for the check to the matching optional parameter
        //ex. getFileLocationInfo(RowCheckTypes.setID, id: 2) will return all rows with a set_id of value '2'
        public static List<FileLocationInfo> getFileLocationInfo(RowCheckTypes addChecks = 0, int attributeid = 99999999, string filepath = null, DateTime? addedon = null, int watchid = 99999999, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.FileLocation))
            {
                addedChecks += " WHERE ";
                addedChecks += getCheckString(addChecks);
            }
            var fileLocationList = new List<FileLocationInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_file_locations " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.attributeID: command.Parameters.AddWithValue("@id", attributeid); break;
                        case RowCheckTypes.filePath: command.Parameters.AddWithValue("@fp", filepath); break;                     
                        case RowCheckTypes.watchedFromID: command.Parameters.AddWithValue("@wid", watchid); break;
                        default: break;
                    };

                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var fileLocationHolder = new FileLocationInfo();
                        fileLocationHolder.LocationID = reader.GetInt32(0);
                        fileLocationHolder.AttributeID = reader.GetInt32(1);
                        fileLocationHolder.TimeDateAdded = reader.GetDateTime(2);
                        fileLocationHolder.WatchedDirectoryID = reader.GetInt32(3);
                        fileLocationHolder.FilePath = reader.GetString(4);
                        fileLocationHolder.Status = reader.GetString(5);                        
                        fileLocationList.Add(fileLocationHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return fileLocationList;
        }

        //Returns a collection of objects each of which can hold the contents of one row of the table
        //By default returns all rows in table, can pass optional check parameter to return a subset instead
        //To do so, set the value of addChecks to the enum type that matches the table column you want to check against, then set the value for the check to the matching optional parameter
        //ex. getBackupSetInfo(RowCheckTypes.setID, id: 2) will return all rows with a set_id of value '2'
        public static List<BackupSetInfo> getBackupSetInfo(RowCheckTypes addChecks = 0, int id = 999999, string dirpath = null, DateTime? lastUpdate = null, int filecount = 99999999, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.BackupSet))
            {
                addedChecks += " WHERE ";
                addedChecks += getCheckString(addChecks);
            }
            var backupSetList = new List<BackupSetInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_backup_sets " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.setID: command.Parameters.AddWithValue("@sid", id); break;
                        case RowCheckTypes.backupPath: command.Parameters.AddWithValue("@bp", dirpath); break;
                        case RowCheckTypes.lastUpdate: command.Parameters.AddWithValue("@lu", lastUpdate); break;
                        case RowCheckTypes.fileCount: command.Parameters.AddWithValue("@fc", filecount); break;
                        default: break;
                    };

                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var backupInfoHolder = new BackupSetInfo();
                        backupInfoHolder.BackupSetID = reader.GetInt32(0);
                        backupInfoHolder.LastUpdatedOn = reader.IsDBNull(1) ? null : (DateTime?)reader.GetDateTime(1);                        
                        backupInfoHolder.BackupDirectoryPath = reader.GetString(2);
                        backupInfoHolder.Status = reader.GetString(3);
                        backupInfoHolder.TimeDateAdded = reader.GetDateTime(4);
                        backupInfoHolder.FilesInBackup = reader.GetInt32(5);
                        backupInfoHolder.LastNumberAssignedToFile = reader.GetInt32(6);
                        backupSetList.Add(backupInfoHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return backupSetList;
        }

        //Adds a row to the table DICOM_backup_lookup which is the attribute id(id column from DICOM_file_attributes)
        //and the backup set id(set_id column from DICOM_backup_sets)
        //Represents that a file with that attribute id has been included in a backup for that set_id
        public static void setBackupLookupTablePair(int a_id, int s_id, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string insertString = "INSERT INTO DICOM_backup_lookup (attribute_id, set_id, time_date_added)"
                + "VALUES (@id, @sid, datetime('now'))";

                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@id", a_id);
                    command.Parameters.AddWithValue("@sid", s_id);
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Returns a collection of objects each of which can hold the contents of one row of the table
        //By default returns all rows in table, can pass optional check parameter to return a subset instead
        //To do so, set the value of addChecks to the enum type that matches the table column you want to check against, then set the value for the check to the matching optional parameter
        //ex. getBackupLookupInfo(RowCheckTypes.attributeID, id: 14) will return all rows with a attribute_id of value '14'
        public static List<BackupLookupInfo> getBackupLookupInfo(RowCheckTypes addChecks = 0, int id = 99999999, int set_id = 99999999, DateTime? timeadded = null, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.BackupLookupTable))
            {
                addedChecks += " WHERE ";
                addedChecks += getCheckString(addChecks);
            }
            var backupLookupList = new List<BackupLookupInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_backup_sets " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.attributeID: command.Parameters.AddWithValue("@id", id); break;
                        case RowCheckTypes.setID: command.Parameters.AddWithValue("@sid", set_id); break;
                        case RowCheckTypes.timeDateAfter: command.Parameters.AddWithValue("@td", timeadded); break;
                        case RowCheckTypes.timeDateBefore: command.Parameters.AddWithValue("@td", timeadded); break;
                        default: break;
                    };

                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var backupLookupInfoHolder = new BackupLookupInfo();
                        backupLookupInfoHolder.AttributeSetID = reader.GetInt32(0);
                        backupLookupInfoHolder.BackupSetID = reader.GetInt32(1);
                        backupLookupInfoHolder.TimeDateAdded = reader.GetDateTime(2);
                        backupLookupList.Add(backupLookupInfoHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return backupLookupList;
        }

        //Returns the dir_id value from the DICOM_watch_directories table for a passed directory path, returns null if not found
        public static string getWatchedID(string dirPath, DBSelection? db = null)
        {
            string watchedID = null;
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area                

                string searchString = "SELECT dir_id FROM DICOM_watch_directories" +
                    " WHERE directory_path=@sDir";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@sDir", dirPath);
                    var reader = command.ExecuteReader();
                    if(reader.Read())
                    {
                        watchedID = reader.GetInt32(0).ToString();
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return watchedID;
        }

        //Returns by reference (via the List<string> fileLocations variable) a collection of file paths(from the file_path column of DICOM_file_locations) that match a passed attribute_id (related value to id from DICOM_file_attributes)
        //Can set an additional check thru addChecks
        //ex. getFilesForAttribute(2, filePaths, RowCheckTypes.isEnabled) adds values to the collection filePaths that had a attribute_id of '2' and currently_active of 'TRUE'
        public static void getFilesForAttribute(int id, List<string> fileLocations, RowCheckTypes addChecks = 0, int watchedID = 9999999, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.FileLocation))
            {
                addedChecks += " AND ";
                addedChecks += getCheckString(addChecks);
            }
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT file_path From DICOM_file_locations" +
                    " WHERE attribute_id=@id " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.watchID: command.Parameters.AddWithValue("@wid", watchedID); break;                        
                        default: break;
                    };
                    command.Parameters.AddWithValue("@id", id);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        fileLocations.Add(reader.GetString(0));
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Returns a collection of objects each of which can hold the contents of one row of the table
        //By default returns all rows in table, can pass optional check parameter to return a subset instead
        //To do so, set the value of addChecks to the enum type that matches the table column you want to check against, then set the value for the check to the matching optional parameter
        //ex. getWatchedInfo(RowCheckTypes.isDisabled) will return all rows with a currently_active of 'FALSE'
        public static List<WatchedDirInfo> getWatchedInfo(RowCheckTypes addChecks = 0, int id = 999999, string dirpath = null, string status = null, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.WatchList))
            {
                addedChecks += " WHERE ";
                addedChecks += getCheckString(addChecks);                
            }
            var watchList = new List<WatchedDirInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_watch_directories" + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.watchID: command.Parameters.AddWithValue("@dID", id); break;
                        case RowCheckTypes.dirPath: command.Parameters.AddWithValue("@dp", dirpath); break;
                        default: break;
                    };
                    
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var watchInfoHolder = new WatchedDirInfo();
                        watchInfoHolder.DirectoryID = reader.GetInt32(0);
                        watchInfoHolder.TimeDateAdded = reader.GetDateTime(1);
                        watchInfoHolder.DirectoryPath = reader.GetString(2);
                        watchInfoHolder.Status = reader.GetString(3);                        
                        watchList.Add(watchInfoHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return watchList;
        }

        //Returns a collection of objects which represent the priority order of watched directories.
        public static List<WatchedOrderInfo> getWatchedOrderingInfo(RowCheckTypes addChecks = 0, int watchID = 99999999, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.WatchOrder))
            {
                addedChecks += " WHERE ";
                addedChecks += " watch_id=@wid ";
            }
            var watchList = new List<WatchedOrderInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_watch_order " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.watchID: command.Parameters.AddWithValue("@wid", watchID); break;                        
                        default: break;
                    };

                    var reader = command.ExecuteReader(); //test
                    while (reader.Read())
                    {
                        var watchOrderHolder = new WatchedOrderInfo();                        
                        watchOrderHolder.WatchDirectoryPositionInOrder = reader.GetInt32(0);
                        watchOrderHolder.WatchID = reader.GetInt32(1);
                        watchList.Add(watchOrderHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return watchList;
        }

        //Returns true if the database table DICOM_watch_directories has a value in the dir_id column that matches the passed value
        //Can add an additional check for the currently_active column by using addCheck
        public static bool checkWatchID(int id, RowCheckTypes addCheck = 0, DBSelection? db = null)
        {
            bool idRecordExists;
            string addedChecks = "";
            if (addCheck == RowCheckTypes.isEnabled || addCheck== RowCheckTypes.isDisabled)
            {
                addedChecks += " AND ";
                addedChecks += getCheckString(addCheck);
            }
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT dir_id From DICOM_watch_directories" +
                    " WHERE dir_id = @id " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@id", id);                    
                    var reader = command.ExecuteReader();
                    idRecordExists = reader.Read();
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return idRecordExists;
        }

        //Returns true if the database table DICOM_backup_sets has a value in the set_id column that matches the passed value
        //Can add an additional check for the currently_active column by using addCheck
        public static bool checkBackupSetID(int id, RowCheckTypes addCheck = 0, DBSelection? db = null)
        {
            bool idRecordExists;
            string addedChecks = "";
            if (addCheck == RowCheckTypes.isEnabled || addCheck == RowCheckTypes.isDisabled)
            {
                addedChecks += " AND ";
                addedChecks += getCheckString(addCheck);
            }
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT dir_id From DICOM_backup_sets" +
                    " WHERE set_id = @sid " + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@sid", id);
                    var reader = command.ExecuteReader();
                    idRecordExists = reader.Read();
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return idRecordExists;
        }

        //Returns a collection of type <int> that contains all the attribute ids from the specified table
        //If an invalid table (like one that holds no attribute id values, like DICOM_watch_directories) is specified it will return null
        //Can add an additional check through addCheck to only return values that also have a certain set_id in the row (currently only applies to DICOM_backup_lookup)
        public static List<int> getAllAttributeSetIDs(TableList tablecheck, RowCheckTypes addCheck = 0, int set_id = 9999999, DBSelection? db = null)
        {
            string rowid = "";
            string tableid = "";
            string addedCheck = "";
            switch(tablecheck)
            {
                case TableList.BackupLookupTable:
                {
                    rowid += " attribute_id ";
                    tableid += "DICOM_backup_lookup ";
                    if (addCheck == RowCheckTypes.setID) { addedCheck += " WHERE set_id=@sid "; }
                    break;
                }
                case TableList.FileAttribute:
                {
                    rowid += " attribute_id ";
                    tableid += " DICOM_file_attributes ";
                    break;
                }
                case TableList.FileLocation:
                {
                    rowid += " attribute_id ";
                    tableid += "DICOM_file_locations ";
                    break;
                }
                default: return null;
            }
            var attribIDs = new List<int>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT " + rowid + " From " + tableid + addedCheck;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    if(addCheck == RowCheckTypes.setID){command.Parameters.AddWithValue("@sid", set_id);}
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        attribIDs.Add(reader.GetInt32(0));
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return attribIDs;
        }

        public static List<AttributeInfo> getAttributeInfo(RowCheckTypes addChecks = 0, int id = 99999999, string name = null, string pid = "", string hash = null, int filesize = 0, string datetaken = null, DateTime? dateadded = null, DBSelection? db = null)
        {
            string addedChecks = "";
            if (addChecks.checkValidaton(TableList.FileAttribute))
            {
                addedChecks += " WHERE ";
                addedChecks += getCheckString(addChecks);
            }
            var attributeList = new List<AttributeInfo>();
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * From DICOM_file_attributes" + addedChecks;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    switch (addChecks)
                    {
                        case RowCheckTypes.attributeID: command.Parameters.AddWithValue("@id", id); break;
                        case RowCheckTypes.patientName: command.Parameters.AddWithValue("@pn", ("%" + name + "%")); break;
                        case RowCheckTypes.patientID: command.Parameters.AddWithValue("@pid", pid); break;
                        case RowCheckTypes.studyDate: command.Parameters.AddWithValue("@sd", datetaken); break;
                        case RowCheckTypes.hash: command.Parameters.AddWithValue("@fh", hash); break;
                        case RowCheckTypes.fileSize: command.Parameters.AddWithValue("@fs", filesize); break;
                        case RowCheckTypes.timeDateBefore: command.Parameters.AddWithValue("@td", dateadded.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")); break;
                        case RowCheckTypes.timeDateAfter: command.Parameters.AddWithValue("td", dateadded.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")); break;
                        default: break;
                    };

                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var attributeInfoHolder = new AttributeInfo();
                        attributeInfoHolder.AttributeSetID = reader.GetInt32(0);
                        attributeInfoHolder.DateTaken = reader.IsDBNull(1) ? null : (DateTime?) reader.GetDateTime(1);//Need to add handling for when this info is null from being redacted
                        attributeInfoHolder.TimeTaken = reader.IsDBNull(2) ? null : (DateTime?) reader.GetDateTime(2);//Need to add handling for when this info is null from being redacted
                        attributeInfoHolder.SeriesDescription = reader.GetString(3);
                        attributeInfoHolder.PatientFullName = reader.GetString(4);
                        attributeInfoHolder.PatientID = reader.GetString(5);
                        attributeInfoHolder.StudyID = reader.GetString(6);
                        attributeInfoHolder.SeriesID = reader.GetString(7);
                        attributeInfoHolder.InstanceID = reader.GetString(8);
                        attributeInfoHolder.Hash = reader.GetString(9);
                        attributeInfoHolder.FileSize = reader.GetInt32(10);
                        attributeInfoHolder.Time_Date_Added = reader.GetDateTime(11);
                        attributeList.Add(attributeInfoHolder);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return attributeList;
        }

        //Checks if a filepath is already indexed in the database
        public static bool checkIfFilePathInDB(string fpath, RowCheckTypes addCheck = 0, DBSelection? db = null)
        {
            bool fileExists;
            string fp = fpath;
            string optionalCheckString = null;

            if (addCheck == RowCheckTypes.isDisabled) { optionalCheckString = " AND currently_active = 'FALSE' "; }
            if (addCheck == RowCheckTypes.isEnabled) { optionalCheckString = " AND currently_active = 'TRUE' "; }
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT * FROM DICOM_file_locations " +
                    " WHERE file_path = @fp " + optionalCheckString;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@fp", fp);
                    var reader = command.ExecuteReader();
                    fileExists = reader.Read();
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return fileExists;
        }

        //Adds a new entry to the DICOM_changelog table, tracks any detected changes to files indexed
        public static void addToChangeLog(string ChangeType, string EffectedFile1, string EffectedFile2, string AdditionalNote, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "INSERT INTO DICOM_changelog (changetype, effected_file_1, effected_file_2, additional_note)" 
                    + " VALUES (@ct, @ef1, @ef2, @an)";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@ct", ChangeType);
                    command.Parameters.AddWithValue("@ef1", EffectedFile1);
                    command.Parameters.AddWithValue("@ef2", EffectedFile2);
                    command.Parameters.AddWithValue("@an", AdditionalNote);
                    command.ExecuteNonQuery();                    
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        //Returns a list of the attribute ID's of DICOM files that have a number of copies passed the redundancyLimit variable
        public static List<int> getRedundantFileCopies(int redundancyLimit, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT attribute_id FROM DICOM_file_locations " 
                    + "GROUP BY attribute_id HAVING COUNT(attribute_id) > @rl";
                var attrList = new List<int>();

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@rl", redundancyLimit);
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        attrList.Add(reader.GetInt32(0));
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;

                return attrList;
            }
        }

        //The table DICOM_misc_values is meant to hold values for unique variables between runtimes. Primary key is the variable name
        //and can store integer, floating point, or character strings in seperate fields for that variable.
        public static void addMiscValue(string nameOfVariable, int? integerValue = null, double? floatValue = null, string textValue = null, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string insertString = "INSERT INTO DICOM_misc_values (variable_name";                

                string valuesString = " VALUES (@vn";

                if (integerValue != null) { insertString += ", integer_value"; valuesString += ", @iv"; }
                if (floatValue != null) { insertString += ", float_value"; valuesString += ", @fv"; }
                if (textValue != null) { insertString += ", text_value"; valuesString += ", @tv"; }

                insertString += ") ";
                valuesString += ")";

                insertString += valuesString;

                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@vn", nameOfVariable);
                    if(integerValue != null) { command.Parameters.AddWithValue("@iv", integerValue); }
                    if(floatValue != null ) { command.Parameters.AddWithValue("@fv", floatValue); }
                    if(textValue != null) { command.Parameters.AddWithValue("@tv", textValue); }
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        public static void updateMiscValue(string nameOfVariable, int? integerValue = null, double? floatValue = null, string textValue = null, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string insertString = "UPDATE DICOM_misc_values SET " ;

                string whereString = " WHERE variable_name=@vn";

                if (integerValue != null) { insertString += " integer_value=@iv ";  }
                if (floatValue != null) { insertString += " float_value=@fv ";  }
                if (textValue != null) { insertString += " text_value=@tv ";  }
                                
                insertString += whereString;

                using (SQLiteCommand command = new SQLiteCommand(insertString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@vn", nameOfVariable);
                    if (integerValue != null) { command.Parameters.AddWithValue("@iv", integerValue); }
                    if (floatValue != null) { command.Parameters.AddWithValue("@fv", floatValue); }
                    if (textValue != null) { command.Parameters.AddWithValue("@tv", textValue); }
                    command.ExecuteNonQuery();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
        }

        public static int? getMiscInt(string nameOfVariable, DBSelection? db = null)
        {
            int? miscInteger = null;

            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area
                                
                string searchString = "SELECT integer_value From DICOM_misc_values" +
                    " WHERE variable_name=@vn";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@vn", nameOfVariable);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        miscInteger = reader.GetInt32(0);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return miscInteger;
        }

        public static double? getMiscDouble(string nameOfVariable, DBSelection? db = null)
        {
            double? miscDouble = null;

            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT float_value From DICOM_misc_values" +
                    " WHERE variable_name=@vn";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@vn", nameOfVariable);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        miscDouble = reader.GetDouble(0);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return miscDouble;
        }

        public static string getMiscText(string nameOfVariable, DBSelection? db = null)
        {
            string miscText = null;

            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT text_value From DICOM_misc_values" +
                    " WHERE variable_name=@vn";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {
                    command.Parameters.AddWithValue("@vn", nameOfVariable);
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        miscText = reader.GetString(0);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return miscText;
        }
        
        public static void setRedundancyLimit(int limitValue)
        {
            if (SQLiteDatabase.getMiscInt("RedundancyControlLimit") != null)
            {
                updateMiscValue("RedundancyControlLimit", limitValue);
            }
            else { addMiscValue("RedundancyControlLimit", limitValue); }
        }

        //Gets the count of rows in a table. Can set addCheck variable to isEnabled or isDisabled to count only rows 
        //currently listed as such if a table has that property (addCheck will be ignored if table doesn't have such a column).
        public static int getRowCountForTable(TableList tablecheck, RowCheckTypes addCheck = 0, DBSelection? db = null)
        {            
            string tableid = "";
            string addedCheck = "";
            switch (tablecheck)
            {
                case TableList.BackupLookupTable:
                    {                        
                        tableid += " DICOM_backup_lookup ";
                        if (addCheck == RowCheckTypes.setID) { addedCheck += " WHERE set_id=@sid "; }
                        break;
                    }
                case TableList.FileAttribute:
                    {                        
                        tableid += " DICOM_file_attributes ";
                        break;
                    }
                case TableList.FileLocation:
                    {                        
                        tableid += "DICOM_file_locations ";
                        if (addCheck == RowCheckTypes.isEnabled) { addedCheck += " WHERE currently_active='TRUE' "; }
                        if (addCheck == RowCheckTypes.isDisabled) { addedCheck += " WHERE currently_active='FALSE' "; }
                        break;
                    }
                case TableList.BackupSet:
                    {
                        tableid += " DICOM_backup_sets ";
                        if (addCheck == RowCheckTypes.isEnabled) { addedCheck += " WHERE currently_active='TRUE' "; }
                        if (addCheck == RowCheckTypes.isDisabled) { addedCheck += " WHERE currently_active='FALSE' "; }
                        break;
                    }
                case TableList.WatchList:
                    {
                        tableid += " DICOM_watch_directories ";
                        if (addCheck == RowCheckTypes.isEnabled) { addedCheck += " WHERE currently_active='TRUE' "; }
                        if (addCheck == RowCheckTypes.isDisabled) { addedCheck += " WHERE currently_active='FALSE' "; }
                        break;
                    }
                case TableList.ChangeLog:
                    {
                        tableid += " DICOM_changelog ";
                        break;
                    }
                default: return 0;
            }

            int rowCount = 0;

            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "SELECT count(*) From " + tableid + addedCheck;

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {                    
                    var reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        rowCount = reader.GetInt32(0);
                    }
                    reader.Close();
                }
                ResourceProtector.CurrentRunningQueries--;
            }
            return rowCount;
        }

        public static void removeWatchDir(int watchID, DBSelection? db = null)
        {
            using (SQLiteConnection mydbConnection = new SQLiteConnection(defaultConnectionString))
            {
                SQLiteConnection dbSelectConnection;
                while (ResourceProtector.DBSourceChangeWaiting) { Thread.Sleep(25); }

                //protected area
                lock (globalDBLock)
                {
                    ResourceProtector.CurrentRunningQueries++;
                    if (db == null) { db = ResourceProtector.MainDBSelector; }
                    if (db == DBSelection.InMemory && ResourceProtector.InMemoryDBActive) { dbSelectConnection = ResourceProtector.InMemoryDBConnection; }
                    else { dbSelectConnection = mydbConnection; dbSelectConnection.Open(); }
                }
                //end of protected area

                string searchString = "DELETE FROM DICOM_watch_directories WHERE dir_id=@id";

                using (SQLiteCommand command = new SQLiteCommand(searchString, dbSelectConnection))
                {                    
                    command.Parameters.AddWithValue("@id", watchID);
                    command.ExecuteNonQuery();
                }

                var tempHolder = getWatchedOrderingInfo(RowCheckTypes.watchID, watchID);

                string searchString2 = "SELECT watch_id FROM DICOM_watch_order WHERE order_position > @op";

                //var watchList = new List<WatchedOrderInfo>();
                var watchList = new List<int>();

                using (SQLiteCommand command2 = new SQLiteCommand(searchString2, dbSelectConnection))
                {
                    
                    command2.Parameters.AddWithValue("@op", tempHolder[0].WatchDirectoryPositionInOrder);
                    var reader = command2.ExecuteReader();
                    while (reader.Read())
                    {
                        int temp;
                        //var watchOrderHolder = new WatchedOrderInfo();
                        //watchOrderHolder.WatchDirectoryPositionInOrder = reader.GetInt32(0);
                        temp = reader.GetInt32(0);
                        watchList.Add(temp);
                    }
                    reader.Close();
                }

                for (int i = 0; i < watchList.Count; i++)
                {
                    setWatchOrderOfPreference(watchList[i], true);
                }

                var fileLocHolder = getFileLocationInfo(RowCheckTypes.watchedFromID, watchid: watchID);
                for (int j = 0; j < fileLocHolder.Count(); j++)
                {
                    removeFileLocation(fileLocHolder[j].FilePath, fileLocHolder[j].AttributeID);
                }

                string searchString3 = "DELETE FROM DICOM_watch_order WHERE watch_id=@id";

                using (SQLiteCommand command3 = new SQLiteCommand(searchString3, dbSelectConnection))
                {
                    command3.Parameters.AddWithValue("@id", watchID);
                    command3.ExecuteNonQuery();
                }

                ResourceProtector.CurrentRunningQueries--;
            }
        }

    }
}
