using System.IO.Compression;
using System.IO;
using System.Text;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using System.Globalization;

public class StringUtility:MonoBehaviour {
    public static byte[] CompressString(string inputString) {
        byte[] inputBytes = Encoding.UTF8.GetBytes(inputString);

        using (MemoryStream outputStream = new MemoryStream()) {
            using (GZipStream gzipStream = new GZipStream(outputStream,CompressionMode.Compress)) {
                gzipStream.Write(inputBytes,0,inputBytes.Length);
            }
            return outputStream.ToArray();
        }
    }

    public static string DecompressString(byte[] compressedBytes) {
        using (MemoryStream inputStream = new MemoryStream(compressedBytes)) {
            using (GZipStream gzipStream = new GZipStream(inputStream,CompressionMode.Decompress)) {
                using (StreamReader streamReader = new StreamReader(gzipStream,Encoding.UTF8)) {
                    return streamReader.ReadToEnd();
                }
            }
        }
    }

    public static bool IsValidFileNameForDate(string value) {
        DateTime dateTime;
        bool isValidFileName = false;

        if (value.Length < 15) {
            return isValidFileName;
        }

        try {
            isValidFileName = DateTime.TryParseExact(value.Substring(0,15),"ddMMyyyy_HHmmss",null,System.Globalization.DateTimeStyles.None,out dateTime);
        } catch (FormatException ex) {
            Debug.LogError("ERROR: parsing date: " + ex.Message);
        }
        return isValidFileName;
    }

    public static string GetReadableFileName(string value) {
        DateTime dateTime = DateTime.ParseExact(value,"ddMMyyyy_HHmmss",null);
        string readableDate = dateTime.ToString("MMMM-dd-yyyy h-mm-ss tt");
        return readableDate;
    }

    public static string OptimizeJson(string json) {
        StringBuilder optimizedJson = new StringBuilder();
        bool isInString = false;
        bool isWhiteSpace = false;

        foreach (char c in json) {
            if (c == '\"') {
                isInString = !isInString;
            }

            if (Char.IsWhiteSpace(c) && !isInString) {
                isWhiteSpace = true;
            } else {
                if (isWhiteSpace) {
                    optimizedJson.Append(' ');
                    isWhiteSpace = false;
                }
                optimizedJson.Append(c);
            }
        }

        return optimizedJson.ToString();
    }

    public static string RemoveUnderScoreAndDashWithSpace(string value) {
        //remove "_" and "-" charachters from string
        return value.Replace("_"," ").Replace("-"," ").Trim();
    }

    public static string RemoveNumbers(string value) {
        //remove any numbers if any persent in string
        return Regex.Replace(value,@"\d",string.Empty).Trim();
    }

    public static string ConvertToTitleCase(string value) {
        //convert string into Title Case
        TextInfo textInfo = new CultureInfo("en-US",false).TextInfo;
        return textInfo.ToTitleCase(value.ToLower());
    }

    public static void CopyToClipboard(string content) {
        GUIUtility.systemCopyBuffer = content;
    }
    public static bool IsValidateMobileNumber(string phoneNumber) {
        // Remove any non-digit characters from the phone number
        string cleanedNumber = Regex.Replace(phoneNumber,@"[^\d]","");

        // Check if the cleaned number has exactly 10 digits
        return cleanedNumber.Length == 10;
    }

    public static bool IsValidateEmail(string email) {
        // Regular expression pattern for validating email addresses
        string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        // Check if the email matches the pattern
        return (Regex.IsMatch(email,emailPattern));
    }

}//StringCompression class end.