using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace WebcamFtpUploader
{
    class Program
    {
        static async Task Main()
        {
            var cameraBaseAddress = Environment.GetEnvironmentVariable("CAMERA_BASE_ADDRESS");
            var cameraUsername = Environment.GetEnvironmentVariable("CAMERA_USERNAME");
            var cameraPassword = Environment.GetEnvironmentVariable("CAMERA_PASSWORD");

            var wundergroundDeviceId = Environment.GetEnvironmentVariable("WUNDERGROUND_CAM_ID");
            var wundergroundDeviceKey = Environment.GetEnvironmentVariable("WUNDERGROUND_CAM_KEY");
            var uploadInterval = int.Parse(Environment.GetEnvironmentVariable("UPLOAD_INTERVAL_MS"));

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{cameraBaseAddress}")
            };

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{cameraUsername}:{cameraPassword}")));

            while (true)
            {
                using var imgRequest = await httpClient.GetAsync("/ISAPI/Streaming/channels/101/picture");
                if(!imgRequest.IsSuccessStatusCode)
                {
                    await Task.Delay(1000);
                    continue;
                }
                using var imgStream = await imgRequest.Content.ReadAsStreamAsync();

                var ftp = (FtpWebRequest)WebRequest.Create("ftp://webcam.wunderground.com/image.jpg");
                ftp.Credentials = new NetworkCredential(wundergroundDeviceId, wundergroundDeviceKey);
                ftp.Method = WebRequestMethods.Ftp.UploadFile;
                ftp.UseBinary = true;
                ftp.UsePassive = true;
                ftp.ContentLength = imgRequest.Content.Headers.ContentLength.Value;

                using var ftpRequestStream = await ftp.GetRequestStreamAsync();
                await imgStream.CopyToAsync(ftpRequestStream);
                using var response = (FtpWebResponse)await ftp.GetResponseAsync();

                Console.WriteLine($"{response.ResponseUri} uploaded: {response.StatusCode}: {response.StatusDescription}");

                await Task.Delay(uploadInterval);
            }
        }
    }
}
