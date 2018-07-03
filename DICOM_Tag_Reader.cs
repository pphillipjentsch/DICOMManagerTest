using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EvilDICOM.Core;

namespace DICOM_Manager
{
    class DICOMTagReader
    {
        public class TagValuePair
        {
            public List<string> TagID;
            public List<string> TagDescription;
            public List<string> Value;

            public TagValuePair()
            {
                TagID = new List<string>();
                TagDescription = new List<string>();
                Value = new List<string>();
            }
        }

        //Scans all the tags in the DICOM file, doesn't print the results. Use scanDICOM(fpath, null, true) to print.
        public static TagValuePair scanDICOM(string fpath)
        {
            return scanDICOM(fpath, null);
        }

        //Scans the prespecified tags from the DICOM file, doesn't print the results. Use scanDICOM(fpath, tags, true) to print.
        public static TagValuePair scanDICOM(string fpath, List<string> tags)
        {
            return scanDICOM(fpath, tags, false);
        }

        //Scans the DICOM file at fpath. If tags != null it will only search the specified tags, if null it will scan all found tags.
        //If printResults is true it will print the resultant tag, tag description, and value to console.
        public static TagValuePair scanDICOM(string fpath, List<string> tags, bool printResults)
        {
            TagValuePair dcmTagsAndValues;

            if (tags == null)
            {
                dcmTagsAndValues = getAllDICOMTagsAndValues(fpath);
            }
            else
            {
                dcmTagsAndValues = getSpecificTagsAndData(fpath, tags);
            }

            if (printResults == true) { printTagValuePair(dcmTagsAndValues); }
            return dcmTagsAndValues;
        }

        public static void printTagValuePair(TagValuePair t)
        {
            for (int i = 0; i < t.TagID.Count(); i++)
            {
                Console.Write(t.TagID[i] + " ");
                Console.Write(t.TagDescription[i] + " ");
                Console.WriteLine(t.Value[i]);
            }
        }

        public static TagValuePair getSpecificTagsAndData(string dcmFilePath, List<string> tags)
        {
            TagValuePair tagValues = new TagValuePair();

            var dcmFile = DICOMObject.Read(@dcmFilePath);

            foreach (string tag in tags)
            {
                tagValues.TagID.Add(tag);
                tagValues.TagDescription.Add(EvilDICOM.Core.Dictionaries.TagDictionary.GetDescription(tag));
                tagValues.Value.Add(getDataFromTag(dcmFile, tag));
            }            

            return tagValues;
        }

        //Reads through the entire DICOM file and returns the found tag descriptions and their data.
        //In TagValuePair class the tag ID is at TagValuePair.TagID[i], description is at TagValuePair.TagDescription[i],
        //and the value for that tag is at TagValuePair.Value[i]
        public static TagValuePair getAllDICOMTagsAndValues(string dcmFilePath)
        {
            TagValuePair tagValues = new TagValuePair();

            var dcmFile = DICOMObject.Read(@dcmFilePath);

            for (int i = 0; i < dcmFile.AllElements.Count; i++)
            {
                tagValues.TagID.Add(dcmFile.AllElements[i].Tag.CompleteID);
                tagValues.TagDescription.Add(EvilDICOM.Core.Dictionaries.TagDictionary.GetDescription(dcmFile.AllElements[i].Tag));
                tagValues.Value.Add(getDataFromTag(dcmFile, dcmFile.AllElements[i]));
            }
            return tagValues;
        }

        //Overload to get data by tagID rather then a IDICOMElement
        public static string getDataFromTag(DICOMObject d, string e)
        {
            var tag = d.FindFirst(e);
            if (tag != null) { return getDataFromTag(d, d.FindFirst(e)); }
            else { return null; }
        }

        //Returns a string containing the data from the tag in IDICOMElement e
        public static string getDataFromTag(DICOMObject d, EvilDICOM.Core.Interfaces.IDICOMElement e)
        {
            var outputString = "";
            var vrFromType = EvilDICOM.Core.Dictionaries.VRDictionary.GetVRFromType(e);
            //var vrFromTag = EvilDICOM.Core.Dictionaries.TagDictionary.GetVRFromTag﻿(e);
            //if (vrFromType != vrFromTag) { Console.WriteLine("Inconsistant VR on Tag " + e.ToString() + vrFromType + " and " + vrFromTag); }
            switch (vrFromType)
            {
                case EvilDICOM.Core.Enums.VR.AgeString:
                    var ageString = e as EvilDICOM.Core.Element.AgeString;
                    if (ageString == null) { return null; }
                    var age = ageString.Age.Number.ToString();
                    //Later make a enum type to handle the age and unit, code below is for that
                    //var age = ageString.Age.Number.ToString() + " " + ageString.Age.Units.ToString();
                    return ageString.Age.Number.ToString();
                case EvilDICOM.Core.Enums.VR.ApplicationEntity:
                    var appEntity = e as EvilDICOM.Core.Element.ApplicationEntity;
                    if (appEntity == null) { return null; }
                    return appEntity.Data;
                case EvilDICOM.Core.Enums.VR.AttributeTag:
                    var attributeTag = e as EvilDICOM.Core.Element.AttributeTag;
                    if (attributeTag == null) { return null; }
                    return attributeTag.Data_.ToString();
                case EvilDICOM.Core.Enums.VR.CodeString:
                    var codeString = e as EvilDICOM.Core.Element.CodeString;
                    if (codeString == null) { return null; }
                    return codeString.Data.ToString();
                case EvilDICOM.Core.Enums.VR.Date:
                    var date = e as EvilDICOM.Core.Element.Date;
                    if (date == null) { return null; }
                    DateTime? dateOrNull = date.Data;
                    if (dateOrNull != null)
                    {
                        DateTime correctedDate = dateOrNull.Value;
                        return correctedDate.ToString("yyyy-MM-dd");
                    }
                    return null;
                case EvilDICOM.Core.Enums.VR.DateTime:
                    var dateTime = e as EvilDICOM.Core.Element.DateTime;
                    if (dateTime == null) { return null; }
                    dateOrNull = dateTime.Data;
                    if (dateOrNull != null)
                    {
                        DateTime correctedDate = dateOrNull.Value;
                        return correctedDate.ToString();
                    }
                    return null;
                case EvilDICOM.Core.Enums.VR.DecimalString:
                    var decimalString = e as EvilDICOM.Core.Element.DecimalString;
                    if (decimalString == null) { return null; }
                    foreach (double dselement in decimalString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString=outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.FloatingPointDouble:
                    var floatPD = e as EvilDICOM.Core.Element.FloatingPointDouble;
                    if (floatPD == null) { return null; }
                    foreach (double dselement in floatPD.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.FloatingPointSingle:
                    var floatPS = e as EvilDICOM.Core.Element.FloatingPointSingle;
                    foreach (double dselement in floatPS.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.IntegerString:
                    var iString = e as EvilDICOM.Core.Element.IntegerString;
                    foreach (double dselement in iString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.LongString:
                    var lString = e as EvilDICOM.Core.Element.LongString;
                    foreach (string dselement in lString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.LongText:
                    var lText = e as EvilDICOM.Core.Element.LongText;
                    if (lText == null) { return null; }
                    return lText.Data.ToString();
                case EvilDICOM.Core.Enums.VR.Null:
                    return null;
                case EvilDICOM.Core.Enums.VR.OtherByteString:
                    var oByteString = e as EvilDICOM.Core.Element.OtherByteString;
                    if (oByteString == null) { return null; }
                    foreach (double dselement in oByteString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.OtherFloatString:
                    var oFloatString = e as EvilDICOM.Core.Element.OtherFloatString;
                    if (oFloatString == null) { return null; }
                    foreach (double dselement in oFloatString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.OtherWordString:
                    var oWordString = e as EvilDICOM.Core.Element.OtherWordString;
                    if (oWordString == null) { return null; }
                    if (oWordString.Data_.Count > 20) { return oWordString.Data_.Count.ToString(); }
                    foreach (double dselement in oWordString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.PersonName:
                    var pName = e as EvilDICOM.Core.Element.PersonName;
                    if (pName == null) { return null; }
                    return pName.Data;
                case EvilDICOM.Core.Enums.VR.Sequence:
                    var sequence = e as EvilDICOM.Core.Element.Sequence;
                    if (sequence == null) { return null; }
                    if (sequence.Items.Count == 0) { return "0 Items"; }
                    return sequence.Data.ToString();
                case EvilDICOM.Core.Enums.VR.ShortString:
                    var sString = e as EvilDICOM.Core.Element.ShortString;
                    if (sString == null) { return null; }
                    foreach (string dselement in sString.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.ShortText:
                    var sText = e as EvilDICOM.Core.Element.ShortText;
                    if (sText == null) { return null; }
                    return sText.Data.ToString();
                case EvilDICOM.Core.Enums.VR.SignedLong:
                    var sLong = e as EvilDICOM.Core.Element.SignedLong;
                    if (sLong == null) { return null; }
                    foreach (double dselement in sLong.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.SignedShort:
                    var signedShort = e as EvilDICOM.Core.Element.SignedShort;
                    if (signedShort == null) { return null; }
                    foreach (double dselement in signedShort.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.Time:
                    var time = e as EvilDICOM.Core.Element.Time;
                    if (time == null) { return null; }
                    dateOrNull = time.Data;
                    if (dateOrNull != null)
                    {
                        DateTime correctedTime = dateOrNull.Value;
                        return correctedTime.TimeOfDay.ToString();
                    }
                    return null;
                case EvilDICOM.Core.Enums.VR.UniqueIdentifier:
                    var uID = e as EvilDICOM.Core.Element.UniqueIdentifier;
                    if (uID == null) { return null; }
                    return uID.Data.ToString();
                case EvilDICOM.Core.Enums.VR.Unknown:
                    //var unknown = e as EvilDICOM.Core.Element.Unknown;
                    return "VR Type Unknown";
                case EvilDICOM.Core.Enums.VR.UnlimitedText:
                    var uText = e as EvilDICOM.Core.Element.UnlimitedText;
                    if (uText == null) { return null; }
                    return uText.Data.ToString();
                case EvilDICOM.Core.Enums.VR.UnsignedLong:
                    var uLong = e as EvilDICOM.Core.Element.UnsignedLong;
                    if (uLong == null) { return null; }
                    foreach (double dselement in uLong.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                case EvilDICOM.Core.Enums.VR.UnsignedShort:
                    var uShort = e as EvilDICOM.Core.Element.UnsignedShort;
                    if (uShort == null) { return null; }
                    foreach (double dselement in uShort.Data_)
                    {
                        outputString += dselement.ToString() + " ";
                    }
                    if (outputString.LastIndexOf(' ') == (outputString.Length - 1) && outputString.Length > 0) { outputString = outputString.Remove(outputString.Length - 1, 1); }
                    return outputString;
                default:
                    return "VR Type Check Failure";
            }
        }
    }
}
