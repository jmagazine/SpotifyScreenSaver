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

public class SpotifyManager {
    public static string ClientId { get; set; } = string.Empty;

    public static string ClientSecret { get; set; } = string.Empty;

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
    public async Task<string> SHA256HashAsync(string plain)
    {
        byte[] data = Encoding.UTF8.GetBytes(plain);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = await Task.Run(() => sha256.ComputeHash(data));
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
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

    public async Task<string> GetCode()
    {



        HttpListener listener = StartHttpListener(8080);

        string code = null;

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
                    // Respond with an error if code is empty
                    response.StatusCode = 400;
                    response.Close();
                    return ""; // Return empty string
                }

                // Code is not null here
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

        ClientId = clientId;
        int port = 8080;

        Debug.WriteLine(ClientId);
       
        // Initialize variables for payload
        string redirectUri = $"https://localhost:{port}";
        string scope = "user-read-private user-read-email";
        string codeVerifier = GenerateRandomString();
        string? hash = SHA256HashAsync(codeVerifier).Result;
        string codeChallenge = Base64Encode(Encoding.UTF8.GetBytes(hash));


        using (HttpClient client = new HttpClient())
        {
            try
            {
                // Develop payload
                Dictionary<string, string> codeQueryParams = new Dictionary<string, string>();
                codeQueryParams.TryAdd("client_id", ClientId);
                codeQueryParams.TryAdd("response_type", "code");
                codeQueryParams.TryAdd("redirect_uri", redirectUri);
                codeQueryParams.TryAdd("scope", scope);
                codeQueryParams.TryAdd("code_challenge_method", "S256");
                codeQueryParams.TryAdd("code_challenge", codeChallenge);

                // Create full URL
                string codeQueryUrl = "https://accounts.spotify.com/authorize/?" + QueryParamsToQueryString(codeQueryParams);
                Debug.WriteLine(codeQueryUrl);

                Process.Start(new ProcessStartInfo(codeQueryUrl)
                {
                    UseShellExecute = true
                });

                string code = null;
                while (string.IsNullOrEmpty(code)) {
                    code = GetCode().Result;
                }

                Dictionary<string, string> accessTokenQueryParams = new Dictionary<string, string>();
                accessTokenQueryParams.TryAdd("client_id", clientId);
                accessTokenQueryParams.TryAdd("grant_type", "authorization_code");
                accessTokenQueryParams.TryAdd("code", code);
                accessTokenQueryParams.TryAdd("redirect_uri", redirectUri);
                accessTokenQueryParams.TryAdd("code_verifier", codeVerifier);


                var content = new FormUrlEncodedContent(accessTokenQueryParams);


                HttpResponseMessage message = client.PostAsync(redirectUri, content).Result;
                HttpContent response = message.Content;
                Debug.WriteLine(response.ToString());



            }

            catch (Exception)
            {
                Debug.WriteLine("Failed to initialize network request, quitting...");
                return;
            }
        }
    }
}