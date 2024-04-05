using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Configuration;
using System.Collections.Specialized;
using static System.Formats.Asn1.AsnWriter;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using static System.Net.WebRequestMethods;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

public class SpotifyManager {

    private string AccessToken;




    /// <summary>
    /// This returns a random string of the specified <paramref name="length"/>
    /// </summary>
    /// <param name="length">The desired length of the string.</param>
    /// <returns></returns>
    public string GenerateRandomString(int length = 64)
    {
        Random random = new Random();

        string possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        string output = "";

        for (int i = 0; i < possible.Length; i++)
        {
            output += possible[random.Next(possible.Length)];
        }

        return output;
    }

    /// <summary>
    /// This returns a SHA256 hashing of a given string.
    /// </summary>
    /// <param name="plain">The plain string to encode.</param>
    /// <returns></returns>
    public async Task<byte[]> SHA256HashAsync(string plain)
    {
        byte[] data = Encoding.UTF8.GetBytes(plain);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = await Task.Run(() => sha256.ComputeHash(data));
            return hashBytes;
        }
    }

    /// <summary>
    /// Returns the string representation of a base64 encoding.
    /// </summary>
    /// <param name="input">The byte array to encode.</param>
    /// <returns></returns>
    public string Base64Encode(byte[] input)
    {
        string base64String = Convert.ToBase64String(input)
            .Replace("=", "")
            .Replace("+", "-")
            .Replace("/", "_");

        return base64String;
    }


    public string QueryParamsToQueryString(Dictionary<string, string> response)
    {
        // Initialize string builder
        StringBuilder sb = new StringBuilder();

        // Iterate through keys
        for (int i = 0; i < response.Keys.Count(); i++)
        {
            // concatenate key and value with "="
            string key = response.Keys.ToArray()[i];
            string value = response[key];
            sb.Append(key + "=" + value);
            if (i < response.Count - 1) {
                sb.AppendLine("&");
            }
        }

        // String representation
        return sb.ToString() ;
    }

    public HttpListener StartHttpListener(int port) {

        string baseUrl = $"http://localhost:{port}/";

        // Create a new HttpListener instance
        HttpListener listener = new HttpListener();

        // Add the base URL to the listener prefixes
        listener.Prefixes.Add(baseUrl);

        // Start the listener
        listener.Start();

        Debug.WriteLine("Listening for requests on " + baseUrl);
        return listener;
    }

    public async Task<string> GetCode(HttpListener listener)
    {

        string? code = null;

        // Run until code is received
        while (string.IsNullOrEmpty(code))
        { 
            var context = await listener.GetContextAsync();
            var request = context.Request;
            var response = context.Response;
            // Process the incoming request
            if (request.HttpMethod == "GET" && request.Url.AbsolutePath == "/callback")
            {
                // Handle the Spotify API authorization code here
                code = request.QueryString["code"];
                if (string.IsNullOrEmpty(code))
                {
                    Debug.WriteLine($"Bad request: {request.RawUrl}");
                    // Respond with an error if code is empty
                    response.StatusCode = 400;
                    response.Close();
                    return ""; // Return empty string
                }

                // Code is not null here
                response.StatusCode = 200;
                response.Close();
                Debug.WriteLine($"Code: {code}");
                return code;
            }
            else
            {
                // Respond with a 404 Not Found for unknown routes
                response.StatusCode = 404;
                response.Close();
            }
        }

        return code;
    }


    public SpotifyManager(string clientId)
    {
        Console.WriteLine($"Client id: {clientId}");
        // Fail if client id is null
        if (string.IsNullOrEmpty(clientId)) {
            Exception NullClientIdError = new Exception("Client Id is empty.");
            throw NullClientIdError;
        }
        // Variable initalization
        int PORT = 8080;
        string SPOTIFY_API_PATH = "https://accounts.spotify.com";
               
        // Initialize variables for payload
        string redirectUri = $"http://localhost:{PORT}/callback";
        string scope = "user-read-private user-read-email";
        string codeVerifier = GenerateRandomString();
        byte[] hash = SHA256HashAsync(codeVerifier).Result;
        string codeChallenge = Base64Encode(hash);


        
        try
        {
            HttpClient spotifyClient = new HttpClient();
            spotifyClient.BaseAddress = new Uri(SPOTIFY_API_PATH);

            // Initialize server
            HttpListener listener = StartHttpListener(PORT);
                
            // Develop payload for GET request
            Dictionary<string, string> codeQueryParams = new Dictionary<string, string>();
            codeQueryParams.TryAdd("client_id", clientId);
            codeQueryParams.TryAdd("response_type", "code");
            codeQueryParams.TryAdd("redirect_uri", redirectUri);
            codeQueryParams.TryAdd("scope", scope);
            codeQueryParams.TryAdd("code_challenge_method", "S256");
            codeQueryParams.TryAdd("code_challenge", codeChallenge);

            // Create full URL
            string codeQueryUrl = $"{SPOTIFY_API_PATH}/authorize?{QueryParamsToQueryString(codeQueryParams)}";

            // Open in default browser
            Process.Start(new ProcessStartInfo(codeQueryUrl)
            {
                UseShellExecute = true
            }); ;

            // Wait to receive code
            string? authCode = null;
            while (string.IsNullOrEmpty(authCode)) {
                Task<string> authCodeTask = GetCode(listener);
                authCode = authCodeTask.Result;
            }

            // Create payload for getting the access token
               
            Dictionary<string, string> content = new Dictionary<string, string>
            {
                { "grant_type", "authorization_code" },
                { "client_id" , clientId },
                {"code" , authCode},
                {"redirect_uri" , redirectUri},
                { "code_verifier" , codeVerifier }
            };

            var parameters = new FormUrlEncodedContent(content);
   

            // jsonContent.Headers.Add("Content-Type", "application/x-www-form-urlencoded");

            HttpResponseMessage response = spotifyClient.PostAsync("api/token/", parameters).Result;
            string jsonString =  response.Content.ReadAsStringAsync().Result;
            string[] kvs = jsonString
                .Replace("{", "")
                .Replace("}", "")
                .Replace("\"", "")
                .Split(',');
            Dictionary<string, string> responseDict = new Dictionary<string, string>();
            foreach(string kv in kvs)
            {
                string[] kv_pair = kv.Split(":");
                responseDict[kv_pair[0]] = kv_pair[1];
            }

            Debug.WriteLine(responseDict["access_token"]);
            this.AccessToken = responseDict["access_token"];
            Debug.WriteLine("Complete!");



        }

        catch (Exception e)
        {
            Debug.WriteLine(e);
            Debug.WriteLine("Failed to initialize Spotify Manager, quitting...");
            
            return;
        }
    }
}
