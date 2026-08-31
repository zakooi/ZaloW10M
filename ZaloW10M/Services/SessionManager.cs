using Windows.Storage;
using Newtonsoft.Json;
using ZaloW10M.Core.Models;

namespace ZaloW10M.Services
{
    public class SessionManager
    {
        private const string SESSION_KEY = "ZaloSessionCredentials";
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        public void SaveSession(ZaloCredentials creds)
        {
            if (creds == null) return;
            string json = JsonConvert.SerializeObject(creds);
            _localSettings.Values[SESSION_KEY] = json;
        }

        public ZaloCredentials LoadSession()
        {
            if (_localSettings.Values.TryGetValue(SESSION_KEY, out object val) && val is string json)
            {
                try
                {
                    return JsonConvert.DeserializeObject<ZaloCredentials>(json);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public void ClearSession()
        {
            _localSettings.Values.Remove(SESSION_KEY);
        }
    }
}