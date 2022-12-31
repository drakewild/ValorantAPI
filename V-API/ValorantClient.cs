using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;


/* Credits: 
 * https://github.com/Ponita0/PoniLCU  
 * https://techchrism.github.io/valorant-api-docs/
 */

namespace ValorantAPI {
    public class OnWebsocketEventArgs : EventArgs {
        public string Path { get; set; }

        public string Type { get; set; }

        public dynamic Data { get; set; }
    }

    public class ValorantClient {

        #region Variables
        public enum REQUEST {
            GET, POST, PUT, DELETE, PATCH
        }

        public enum ENDPOINT {
            E_LOCAL, E_PD, E_GLZ, E_SHARED
        }

        public enum REGION {
            latam, br, na, eu, ap, kr
        }

        private static readonly Dictionary<REGION, Tuple<string, string>> Regions = new Dictionary<REGION, Tuple<string, string>>()
        {
            { REGION.latam, new Tuple<string, string>("latam" , "na") },
            { REGION.br, new Tuple<string, string>("br", "na") },
            { REGION.na, new Tuple<string, string>("na", "na")},
            { REGION.eu, new Tuple<string, string>("eu", "eu") },
            { REGION.ap,new Tuple<string, string>( "ap", "ap") },
            {REGION.kr, new Tuple<string, string>("kr", "kr") }
        };

        private static Tuple<string, string> LockFileCredentials = new Tuple<string, string>("login", "pass");

        private readonly Dictionary<string, List<Action<OnWebsocketEventArgs>>> Subscribes = new Dictionary<string, List<Action<OnWebsocketEventArgs>>>();

        private static readonly Dictionary<REQUEST, string> RequestMethods = new Dictionary<REQUEST, string>
        {
            { REQUEST.GET, "GET" },
            { REQUEST.POST, "POST" },
            { REQUEST.PATCH, "PATCH" },
            { REQUEST.DELETE, "DELETE" },
            { REQUEST.PUT, "PUT" }
        };

        private static Dictionary<ENDPOINT, string> Endpoints;
        private static HttpClient client;
        private WebSocket socketConnection;
        private bool b_Connected;
        private string AuthToken;
        private string JWTToken;
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<OnWebsocketEventArgs> OnWebsocketEvent;

        public bool IsConnected => b_Connected;
        #endregion
        public ValorantClient(REGION reg) {
            Endpoints = new Dictionary<ENDPOINT, string> {
                { ENDPOINT.E_LOCAL, "https://127.0.0.1:" },
                { ENDPOINT.E_PD, $"https://pd.{Regions[reg].Item2}.a.pvp.net" },
                { ENDPOINT.E_GLZ, $"https://glz-{Regions[reg].Item1}-1.{Regions[reg].Item2}.a.pvp.net" },
                { ENDPOINT.E_SHARED, $"https://shared.{Regions[reg].Item2}.a.pvp.net" }
            };

            try {
                client = new HttpClient(new HttpClientHandler() {
                    SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls,
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            } catch {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

                client = new HttpClient(new HttpClientHandler() {
                    ServerCertificateCustomValidationCallback = (a, b, c, d) => true
                });
            }
            TryToConnect().ConfigureAwait(true);
        }

        private async Task TryToConnect() {
            int i_Tries = 0;
            while (i_Tries < 3) {
                try {
                    Connect();
                    if (IsConnected) {
                        break;
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }

                i_Tries++;
                await Task.Delay(2500);
            }
        }
        private void Connect() {
            Process[] processes = Process.GetProcessesByName("Valorant");
            if (processes.Length <= 0) {
                b_Connected = false;
                return;
            }

            try {
                LockFileCredentials = GetLockfile();
                if (LockFileCredentials == null) {
                    b_Connected = false;
                    return;
                }

                var byteArray = Encoding.ASCII.GetBytes("riot:" + LockFileCredentials.Item1);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

                socketConnection = new WebSocket("wss://127.0.0.1:" + LockFileCredentials.Item2 + "/", "wamp");
                socketConnection.SetCredentials("riot", LockFileCredentials.Item1, true);
                socketConnection.SslConfiguration.EnabledSslProtocols = SslProtocols.Tls12;
                socketConnection.SslConfiguration.ServerCertificateValidationCallback = (a, b, c, d) => true;

                socketConnection.OnMessage += HandleMessage;
                socketConnection.OnClose += HandleDisconnect;

                socketConnection.Connect();
                socketConnection.Send($"[5, \"OnJsonApiEvent\"]");
                b_Connected = true;
                RSOAuth();

                OnConnected?.Invoke();
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }


        public Task<string> Request(REQUEST method, string URL, ENDPOINT endp, object body = null) {
            if (!IsConnected) {
                throw new InvalidOperationException("Client is not connected to Valorant");
            }

            string s_RequestMethod = RequestMethods[method];

            if (URL[0] != '/') {
                URL = "/" + URL;
            }
            var NewRequest = new HttpRequestMessage();
            string s_CustomURL = Endpoints[endp];

            //You might need to update these. Currently working (12/31/2022)
            NewRequest.Headers.Add("X-Riot-ClientPlatform", "ew0KCSJwbGF0Zm9ybVR5cGUiOiAiUEMiLA0KCSJwbGF0Zm9ybU9TIjogIldpbmRvd3MiLA0KCSJwbGF0Zm9ybU9TVmVyc2lvbiI6ICIxMC4wLjE5MDQyLjEuMjU2LjY0Yml0IiwNCgkicGxhdGZvcm1DaGlwc2V0IjogIlVua25vd24iDQp9");
            NewRequest.Headers.Add("X-Riot-ClientVersion", "release-05.12-21-808353");

            if (endp == ENDPOINT.E_LOCAL) {
                NewRequest.Method = new HttpMethod(s_RequestMethod);
                NewRequest.RequestUri = new Uri(s_CustomURL + $"{LockFileCredentials.Item2}" + URL);
                NewRequest.Content = body == null ? null : new StringContent(body.ToString(), Encoding.UTF8, "application/json");
            } else {
                NewRequest.Method = new HttpMethod(s_RequestMethod);
                NewRequest.RequestUri = new Uri(s_CustomURL + URL);
                NewRequest.Content = body == null ? null : new StringContent(body.ToString(), Encoding.UTF8, "application/json");
                NewRequest.Headers.Add("AUTHORIZATION", $"Bearer {AuthToken}");
                NewRequest.Headers.Add("x-riot-entitlements-jwt", $"{JWTToken}");
            }

            return client.SendAsync(NewRequest).Result.Content.ReadAsStringAsync();
        }

        private Tuple<string, string> GetLockfile() {
            try {
                var localAppdata = Environment.GetEnvironmentVariable("LocalAppData");
                var fullPath = localAppdata + "\\Riot Games\\Riot Client\\Config\\lockfile";

                string s_Lockfile;
                using (var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream)) {
                    s_Lockfile = reader.ReadToEnd();
                }

                if (s_Lockfile.Length <= 0) {
                    throw new InvalidOperationException($"Lockfile is empty");
                }
                var splitContent = s_Lockfile.Split(':');
                return new Tuple<string, string>
                (
                    splitContent[3],
                    splitContent[2]
                );
            } catch (Exception e) {
                throw new InvalidOperationException($"Could not obtain lockfile data: {e})");
            }
        }

        private async void RSOAuth() {
            try {
                AuthToken = JObject.Parse(await Request(REQUEST.GET, "/rso-auth/v1/authorization/access-token", ENDPOINT.E_LOCAL))["token"].ToString();
                JWTToken = JObject.Parse(await Request(REQUEST.GET, "/entitlements/v1/token", ENDPOINT.E_LOCAL))["token"].ToString();
            } catch (Exception e) {
                throw new InvalidOperationException($"Could not get auth tokens: {e}");
            }
        }

        private void HandleMessage(object sender, MessageEventArgs args) {
            if (!args.IsText) return;
            var payload = JsonConvert.DeserializeObject<JArray>(args.Data);

            if (payload.Count != 3) return;
            if ((long)payload[0] != 8 || !((string)payload[1]).Equals("OnJsonApiEvent")) return;

            var ev = (dynamic)payload[2];
            OnWebsocketEvent?.Invoke(new OnWebsocketEventArgs() {
                Path = ev["uri"],
                Type = ev["eventType"],
                Data = ev["eventType"] == "Delete" ? null : ev["data"]
            });
            if (Subscribes.ContainsKey((string)ev["uri"])) {
                foreach (var item in Subscribes[(string)ev["uri"]]) {
                    item(new OnWebsocketEventArgs() {
                        Path = ev["uri"],
                        Type = ev["eventType"],
                        Data = ev["eventType"] == "Delete" ? null : ev["data"]
                    });
                }
            }
        }

        public void Subscribe(string URI, Action<OnWebsocketEventArgs> args) {
            if (!Subscribes.ContainsKey(URI)) {
                Subscribes.Add(URI, new List<Action<OnWebsocketEventArgs>>() { args });
            } else {
                Subscribes[URI].Add(args);
            }
        }

        public void Unsubscribe(string URI, Action<OnWebsocketEventArgs> action) {
            if (Subscribes.ContainsKey(URI)) {
                if (Subscribes[URI].Count == 1) {
                    Subscribes.Remove(URI);
                } else if (Subscribes[URI].Count > 1) {
                    foreach (var item in Subscribes[URI].ToArray()) {
                        if (item == action) {
                            var index = Subscribes[URI].IndexOf(action);
                            Subscribes[URI].RemoveAt(index);

                        }
                    }
                } else {
                    return;
                }
            }
        }
        private async void HandleDisconnect(object sender, CloseEventArgs args) {
            LockFileCredentials = null;
            b_Connected = false;
            socketConnection = null;

            OnDisconnected?.Invoke();

            await TryToConnect();
        }

        public void ClearAllListeners() {
            OnWebsocketEvent = null;
        }
    }
}
