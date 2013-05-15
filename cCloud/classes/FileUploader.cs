using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web;

namespace cCloud {
    /// <summary>
    ///   Send files to a server using POST with the possibility to add custom POST parameters
    /// </summary>
    static class FileUploader {

        // With file
        public static bool UploadFile(String filepath, string[,] postParams = null) {
            try {
                return multipartFormDataSendFile(Init.Singleton.uploadURL, filepath, postParams);
            } catch (Exception e) {
                NotificationManager.Inform("The upload has failed! " + e.Message, NotificationTypes.Warning, true);
                return false;
            }
        }
        private static bool multipartFormDataSendFile(string url, string file, string[,] postParams) {
            // Unique boundary
            string boundary = "----------------------------" + DateTime.Now.Ticks.ToString("x");

            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
            httpWebRequest.ContentType = "multipart/form-data; boundary=" + boundary; // specify the boundary
            httpWebRequest.Method = "POST";
            httpWebRequest.KeepAlive = true;
            httpWebRequest.Credentials = Init.Singleton.netCredentials;

            Stream memStream = new System.IO.MemoryStream(); // internal buffer

            byte[] boundarybytes = System.Text.Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            // template used for each custom POST param
            string formdataTemplate = "\r\n--" + boundary + "\r\nContent-Disposition: form-data; name=\"{0}\";\r\n\r\n{1}";

            for(int i = 0; i <= postParams.GetUpperBound(0); i++) {
                // Create the correct form-data from parameter and write it to memStream
                string formitem = string.Format(formdataTemplate, postParams[i, 0], postParams[i, 1]);
                byte[] formitembytes = System.Text.Encoding.UTF8.GetBytes(formitem);
                memStream.Write(formitembytes, 0, formitembytes.Length);
            }

            memStream.Write(boundarybytes, 0, boundarybytes.Length);

            // Write the file octet stream
            string headerTemplate = "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\n Content-Type: application/octet-stream\r\n\r\n";
            string header = string.Format(headerTemplate, "thefile", file);
            byte[] headerbytes = System.Text.Encoding.UTF8.GetBytes(header);
            memStream.Write(headerbytes, 0, headerbytes.Length);

            // Read the file
            FileStream fileStream = new FileStream(file, FileMode.Open, FileAccess.Read);
            byte[] buffer = new byte[1024];
            int bytesRead = 0;
            while((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0) {
                memStream.Write(buffer, 0, bytesRead);
            }
            memStream.Write(boundarybytes, 0, boundarybytes.Length);
            fileStream.Close();

            // Prepare and send request
            httpWebRequest.ContentLength = memStream.Length;

            Stream requestStream = httpWebRequest.GetRequestStream();

            // copy memStream in a byte array
            memStream.Position = 0;
            byte[] tempBuffer = new byte[memStream.Length];
            memStream.Read(tempBuffer, 0, tempBuffer.Length);
            memStream.Close();

            // Write the request
            requestStream.Write(tempBuffer, 0, tempBuffer.Length);
            requestStream.Close();

            // Get response
            HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
            string responseStr = new StreamReader(response.GetResponseStream()).ReadToEnd();
            httpWebRequest = null;

            if(responseStr.Contains("<b>")) { // <b> => server error returns HTML = error
                NotificationManager.Inform("EXTERNAL PHP: " + responseStr, NotificationTypes.Warning);
                return false;
            } else {
                NotificationManager.Inform("EXTERNAL PHP: " + responseStr);
                return true;
            }
        }

        // Without file
        public static bool CustomPOST(string[,] postParams) {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(Init.Singleton.uploadURL);
            req.Method = "POST";

            // Build POST request
            string customPOST = "";
            if(postParams != null) {
                for(int i = 0; i <= postParams.GetUpperBound(0); i++) {
                    customPOST += "&" + postParams[i, 0] + "=" + postParams[i, 1];
                }
            }
            try {
                return sendPOSTData(customPOST, Init.Singleton.uploadURL);
            }catch(Exception e){
                NotificationManager.Inform("The POST request has failed! " + e.Message, NotificationTypes.Warning, true);
                return false;
            }
        }
        private static bool sendPOSTData(string postParams, string url) {
            byte[] postBytes = Encoding.UTF8.GetBytes(postParams);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = postBytes.Length;
            req.Credentials = Init.Singleton.netCredentials;

            Stream requestStream = req.GetRequestStream();
            requestStream.Write(postBytes, 0, postBytes.Length);
            requestStream.Close();

            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            string responseStr = new StreamReader(response.GetResponseStream()).ReadToEnd();

            if(responseStr.Contains("<b>")) { // <b> => server error returns HTML = error
                NotificationManager.Inform("EXTERNAL PHP: " + responseStr, NotificationTypes.Warning);
                return false;
            } else {
                NotificationManager.Inform("EXTERNAL PHP: " + responseStr);
                return true;
            }
        }
    }
}
