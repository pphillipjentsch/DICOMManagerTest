using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace DICOM_Manager
{
    class fileHash
    {
        public static string getFileHash(string fpath)
        {
            SHA512 shaHasher = SHA512Managed.Create();
            byte[] hashValue;
            string hash;

            FileStream fileStream = new FileStream(fpath, FileMode.Open);
            fileStream.Position = 0;
            hashValue = shaHasher.ComputeHash(fileStream);            
            hash = ByteArrayToString(hashValue);
            fileStream.Close();

            return hash;
        }

        private static string ByteArrayToString(byte[] array)
        {
            string h = "";            
            for (int i = 0; i < array.Length; i++)
            {
                h += String.Format("{0:X2}", array[i]);                
                //if ((i % 4) == 3) h += " ";
            }
            return h;
        }

    }
}
