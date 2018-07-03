using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace DICOM_Manager
{
    public class enumDICOMFiles
    {
        public static List<string> findDICOMInDirAndSubdir(string dir)
        {                        
            DirectoryInfo diTop = new DirectoryInfo(dir);
            var files = new List<string>();
            try
            {
                foreach (var fi in diTop.EnumerateFiles("*.dcm"))
                {
                    try
                    { 
                        files.Add(fi.FullName.ToLower());                        
                    }
                    catch (UnauthorizedAccessException UnAuthTop)
                    {
                        Console.WriteLine("{0}", UnAuthTop.Message);
                    }
                }

                foreach (var di in diTop.EnumerateDirectories("*"))
                {
                    try
                    {
                        foreach (var fi in di.EnumerateFiles("*.dcm", SearchOption.AllDirectories))
                        {
                            try
                            {                                
                                    files.Add(fi.FullName.ToLower());                                
                            }
                            catch (UnauthorizedAccessException UnAuthFile)
                            {
                                Console.WriteLine("UnAuthFile: {0}", UnAuthFile.Message);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException UnAuthSubDir)
                    {
                        Console.WriteLine("UnAuthSubDir: {0}", UnAuthSubDir.Message);
                    }
                }
            }
            catch (DirectoryNotFoundException DirNotFound)
            {
                Console.WriteLine("{0}", DirNotFound.Message);
            }
            catch (UnauthorizedAccessException UnAuthDir)
            {
                Console.WriteLine("UnAuthDir: {0}", UnAuthDir.Message);
            }
            catch (PathTooLongException LongPath)
            {
                Console.WriteLine("{0}", LongPath.Message);
            }

            return files;
        }

        //Checks if the passed directory path exists, returns false if it doesn't, handles exceptions
        public static bool checkDirExists(string dirPath)
        {
            bool existsCheck;
            DirectoryInfo dirInfo;

            try
            {
                dirInfo = new DirectoryInfo(dirPath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            existsCheck = dirInfo.Exists;
            return existsCheck;
        }

        //returns true if file, false if directory
        public static bool checkIfFileOrDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(@path);

            if (attr.HasFlag(FileAttributes.Directory)) {return false;}
            else {return true;}
        }
    }
}
