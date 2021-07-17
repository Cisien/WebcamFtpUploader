using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebcamFtpUploader
{
    class Program
    {
        private static readonly string _cameraBaseAddress = Environment.GetEnvironmentVariable("CAMERA_BASE_ADDRESS");
        private static readonly string _cameraUsername = Environment.GetEnvironmentVariable("CAMERA_USERNAME");
        private static readonly string _cameraPassword = Environment.GetEnvironmentVariable("CAMERA_PASSWORD");

        private static readonly string _wundergroundDeviceId = Environment.GetEnvironmentVariable("WUNDERGROUND_CAM_ID");
        private static readonly string _wundergroundDeviceKey = Environment.GetEnvironmentVariable("WUNDERGROUND_CAM_KEY");
        private static readonly int _uploadInterval = int.Parse(Environment.GetEnvironmentVariable("UPLOAD_INTERVAL_MS"));

        private static readonly Timer _healthcheckTimer = new(HealthCheck);
        private static DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

        static async Task Main()
        {
            Ensure(_cameraBaseAddress, "CAMERA_BASE_ADDRESS");
            Ensure(_cameraUsername, "CAMERA_USERNAME");
            Ensure(_cameraPassword, "CAMERA_PASSWORD");
            Ensure(_wundergroundDeviceId, "WUNDERGROUND_CAM_ID");
            Ensure(_wundergroundDeviceKey, "WUNDERGROUND_CAM_KEY");
            Ensure(_uploadInterval, "UPLOAD_INTERVAL_MS");

            _healthcheckTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri($"{_cameraBaseAddress}")
            };

            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_cameraUsername}:{_cameraPassword}")));

            while (true)
            {
                var cts = new CancellationTokenSource(_uploadInterval / 2);
                using var imgRequest = await httpClient.GetAsync("/ISAPI/Streaming/channels/101/picture", cts.Token);
                if (!imgRequest.IsSuccessStatusCode)
                {
                    var responseBody = await imgRequest.Content.ReadAsStringAsync(cts.Token);
                    Console.WriteLine($"The camera returned an error: {imgRequest.StatusCode}: \n\t{responseBody}");
                    await Task.Delay(1000);
                    continue;
                }
                using var imgStream = await imgRequest.Content.ReadAsStreamAsync(cts.Token);

                var ftp = (FtpWebRequest)WebRequest.Create("ftp://webcam.wunderground.com/image.jpg");
                ftp.Credentials = new NetworkCredential(_wundergroundDeviceId, _wundergroundDeviceKey);
                ftp.Method = WebRequestMethods.Ftp.UploadFile;
                ftp.UseBinary = true;
                ftp.UsePassive = true;
                ftp.ContentLength = imgRequest.Content.Headers.ContentLength.Value;

                using var ftpRequestStream = await ftp.GetRequestStreamAsync();
                await imgStream.CopyToAsync(ftpRequestStream, cts.Token);
                using var response = (FtpWebResponse)await ftp.GetResponseAsync();

                Console.WriteLine($"{response.ResponseUri} uploaded: {response.StatusCode}: {response.StatusDescription}");
                _lastUpdate = DateTimeOffset.UtcNow;
                await Task.Delay(_uploadInterval);
            }
        }

        private static void Ensure(object value, string name)
        {
            if(value is null)
            {
                throw new ArgumentNullException(name);
            }

            if(value is "")
            {
                throw new ArgumentException("Argument missing value", name);
            }
        }

        private static void HealthCheck(object _)
        {
            if ((DateTimeOffset.UtcNow - _lastUpdate).TotalMilliseconds > _uploadInterval * 3)
            {
                Console.WriteLine($"Upload loop appears to have hung or has failed repeatedly for {_uploadInterval * 3}ms, exiting.");
                Environment.Exit(1);
            }
        }
    }
}
