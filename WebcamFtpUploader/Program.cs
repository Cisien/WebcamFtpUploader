using FluentFTP;
using FluentFTP.Helpers;
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

        private static readonly string _pasteKey = Environment.GetEnvironmentVariable("PASTE_KEY");
        private static readonly int _uploadInterval = int.Parse(Environment.GetEnvironmentVariable("UPLOAD_INTERVAL_MS"));

        private static readonly Timer _healthcheckTimer = new(HealthCheck);
        private static DateTimeOffset _lastUpdate = DateTimeOffset.MinValue;

        static async Task Main()
        {
            Console.WriteLine("Starting");
            Ensure(_cameraBaseAddress, "CAMERA_BASE_ADDRESS");
            Ensure(_cameraUsername, "CAMERA_USERNAME");
            Ensure(_cameraPassword, "CAMERA_PASSWORD");
            Ensure(_pasteKey, "PASTE_KEY");
            Ensure(_uploadInterval, "UPLOAD_INTERVAL_MS");

            _healthcheckTimer.Change(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            var cameraClient = new HttpClient
            {
                BaseAddress = new Uri($"{_cameraBaseAddress}")
            };
            cameraClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_cameraUsername}:{_cameraPassword}")));
            var pasteClient = new HttpClient()
            {
                BaseAddress = new Uri("https://paste.cisien.dev/submit/postspecific")
            };
            pasteClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", _pasteKey);

            while (true)
            {
                var cts = new CancellationTokenSource(_uploadInterval / 2);
                try
                {
                    Console.WriteLine($"Beginning Read from WebCam: {cameraClient.BaseAddress}ISAPI/Streaming/channels/101/picture");
                    
                    using var imgRequest = await cameraClient.GetAsync("ISAPI/Streaming/channels/101/picture", cts.Token);
                    if (!imgRequest.IsSuccessStatusCode)
                    {
                        var responseBody = await imgRequest.Content.ReadAsStringAsync(cts.Token);
                        Console.WriteLine($"The camera returned an error: {imgRequest.StatusCode}: \n\t{responseBody}");
                        await Task.Delay(1000);
                        continue;
                    }
                    using var imgStream = await imgRequest.Content.ReadAsStreamAsync(cts.Token);
                    Console.WriteLine("Done reading from webcam");

                    var content = new MultipartFormDataContent();
                    var imgContent = new StreamContent(imgStream);
                    imgContent.Headers.ContentType = imgRequest.Content.Headers.ContentType;
                    content.Add(imgContent, "file", "webcam.jpg");

                    var pasteResponse = await pasteClient.PostAsync("", content, cts.Token);

                    var pasteBody = await pasteResponse.Content.ReadAsStringAsync();
                    if(pasteResponse.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Paste upload completed");
                        _lastUpdate = DateTimeOffset.UtcNow;
                    }

                    Console.WriteLine(pasteBody);

                    await Task.Delay(_uploadInterval);
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Exception: {ex.Message}");
                    await Task.Delay(1000, cts.Token);
                }
            }
        }

        private static void Ensure(object value, string name)
        {
            if (value is null)
            {
                throw new ArgumentNullException(name);
            }

            if (value is "")
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
