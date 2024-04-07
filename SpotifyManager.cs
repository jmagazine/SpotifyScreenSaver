using SpotifyAPI.Web;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
public class SpotifyManager
{

    // ######
    // FIELDS
    // ######

    // Client id for API application
    private string ClientId = "";

    // Access Token to use API
    private string AccessToken = "";

    // Refresh Token
    private string RefreshToken = "";

    // HttpListener to act as server
    private HttpListener Server = new HttpListener();

    // SpotifyClient for making API calls
    private SpotifyClient Client;

    // Authenticator
    private PKCEAuthenticator Authenticator;

    // Timer to refresh the token after 1 hour
    private System.Timers.Timer refreshTokenTimer = new System.Timers.Timer();

    // Check to determine if the SpotifyManager was fully initialized
    private bool clientDidStart = false;

    // User Id of connected Spotify Account.
    private JsonNode currentUserProfile;

    // Exception for when clientDidStart == false;
    private Exception ClientNotInitializedException = new Exception("Cannot perform operation: SpotifyClient has not been initialized.");

    // ######
    // METHODS
    // ######

    /// <summary>
    /// Convert HttpContent to a Dictionary with string key/value pairs.  
    /// </summary>
    /// <param name="content">HttpContent object to convert.</param>
    /// <returns></returns>
    private Dictionary<string, string> HttpContentToDictionary(HttpContent content)
    {

        string contentString = content.ReadAsStringAsync().Result;
        string[] kvs = contentString
                .Replace("{", "")
                .Replace("}", "")
                .Replace("\"", "")
                .Split(',');
        Dictionary<string, string> responseDict = new Dictionary<string, string>();
        foreach (string kv in kvs)
        {
            string[] kv_pair = kv.Split(":");
            responseDict[kv_pair[0]] = kv_pair[1];
        }
        return responseDict;
    }

    /// <summary>
    /// Refreshes the current access token, updating that as well as the refresh token.
    /// </summary>
    /// 
    //private void RefreshAccessToken()
    //{


    //    // Checks that the spotify client has been fully initialized
    //    if (!clientDidStart)
    //    {
    //        Exception e = new Exception("The HTTPClient did not start. Quitting with error...");
    //        throw e;
    //    }
    //    // Build the payload
    //    Dictionary<string, string> content = new Dictionary<string, string>{
    //        {"grant_type", "refresh_token"},
    //        { "refresh_token", this.RefreshToken},
    //        {"client_id",this.ClientId },
    //    };

    //    // Encode using x-www-form-urlencoded
    //    FormUrlEncodedContent payload = new FormUrlEncodedContent(content);

    //    // Get response
    //    HttpResponseMessage response = AuthClient.PostAsync("api/token", payload).Result;

    //    // Ensure successful Status Code
    //    if (!response.IsSuccessStatusCode)
    //    {
    //        Debug.WriteLine($"Error {response.StatusCode}");
    //        return;
    //    }

    //    // Set access token and refresh token
    //    Dictionary<string, string> responseDict = HttpContentToDictionary(response.Content);
    //    this.AccessToken = responseDict["access_token"];
    //    this.RefreshToken = responseDict["refresh_token"];
    //}




    /// <summary>
    /// This returns a random string of the specified <paramref name="length"/>
    /// </summary>
    /// <param name="length">The desired length of the string.</param>
    /// <returns>A randomly generated string.</returns>
    private string GenerateRandomString(int length = 64)
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
    /// <returns>A byte array representing the SHA256 hashing of <paramref name="plain"/>.</returns>
    private byte[] SHA256HashAsync(string plain)
    {
        byte[] data = Encoding.UTF8.GetBytes(plain);

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hashBytes = Task.Run(() => sha256.ComputeHash(data)).Result;
            return hashBytes;
        }
    }

    /// <summary>
    /// Returns the string representation of a base64 encoding.
    /// </summary>
    /// <param name="input">The byte array to encode.</param>
    /// <returns>A string representing the base64Encoding <paramref name="input"/>.</returns>
    private string Base64Encode(byte[] input)
    {
        string base64String = Convert.ToBase64String(input)
            .Replace("=", "")
            .Replace("+", "-")
            .Replace("/", "_");

        return base64String;
    }


    /// <summary>
    /// Converts a dictionary of query parameters to a query string.
    /// </summary>
    /// <param name="parameters">The dictionary representing the parameters.</param>
    /// <returns>A string of query parameters.</returns>
    private string QueryParamsToQueryString(Dictionary<string, string> parameters)
    {
        // Initialize string builder
        StringBuilder sb = new StringBuilder();

        // Iterate through keys
        for (int i = 0; i < parameters.Keys.Count(); i++)
        {
            // concatenate key and value with "="
            string key = parameters.Keys.ToArray()[i];
            string value = parameters[key];
            sb.Append(key + "=" + value);
            if (i < parameters.Count - 1)
            {
                sb.AppendLine("&");
            }
        }

        // String representation
        return sb.ToString();
    }

    /// <summary>
    /// This starts an HTTPListener on the specified port. This is used for the PKCE Authorization code flow.
    /// </summary>
    /// <param name="port">The port to open.</param>
    /// <returns></returns>
    private HttpListener StartHttpListener(int port)
    {

        string baseUrl = $"http://localhost:{port}/";

        // Create a new HttpListener instance
        HttpListener listener = new HttpListener();

        // Add the base URL to the listener prefixes
        listener.Prefixes.Add(baseUrl);

        // Start the listener
        listener.Start();

        Debug.WriteLine($"Listening for requests on  port {port}.");
        return listener;
    }

    /// <summary>
    /// Gets the code to supply when requesting an access token.
    /// </summary>
    /// <param name="listener">HttpListener that the code will be redirected to.</param>
    /// <returns></returns>
    private string GetCode()
    {

        string? code = null;

        // Run until code is received
        while (string.IsNullOrEmpty(code))
        {
            var context = Server.GetContextAsync().Result;
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
                return code;
            }
            else
            {
                // Respond with a 404
                response.StatusCode = 404;
                response.Close();
            }
        }

        return code;
    }

    /// <summary>
    /// Instantiates a SpotifyManager, which handles Spotify API requests on behalf of the client.
    /// </summary>
    /// <param name="clientId">The client id of the Spotify application used when making API requests.</param>
    public SpotifyManager(string clientId)
    {
        // Fail if client id is null
        if (string.IsNullOrEmpty(clientId))
        {
            Exception NullClientIdError = new Exception("Client Id is empty.");
            throw NullClientIdError;
        }
        // Variable initalization
        string REDIRECT_URI_PATH = "http://localhost:8080/";
        string SPOTIFY_AUTH_PATH = "https://accounts.spotify.com";
        string SPOTIFY_API_PATH = "https://api.spotify.com";

        try
        {
            // Initialize listener
            Server.Prefixes.Add(REDIRECT_URI_PATH.ToString());
            Server.Start();
            // Create PKCE login request
            var (verifier, challenge) = PKCEUtil.GenerateCodes();
            LoginRequest loginRequest = new LoginRequest(
                      new Uri(REDIRECT_URI_PATH + "callback"), clientId,
                      LoginRequest.ResponseType.Code
                    )
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = new[] { Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative,
                Scopes.UserLibraryRead, Scopes.UserReadPrivate , Scopes.UserTopRead}
            };
            // Get uri for authentication link
            Uri authUri = loginRequest.ToUri();
            // Open in default browser
            Process.Start(new ProcessStartInfo(authUri.ToString())
            {
                UseShellExecute = true
            }); ;

            // Wait to receive code
            string? authCode = null;
            while (string.IsNullOrEmpty(authCode))
            {
                authCode = GetCode();
            }

            // Request token
            PKCETokenRequest tokenRequest = new PKCETokenRequest(clientId, authCode, new Uri(REDIRECT_URI_PATH + "callback"), verifier);

            // Save AccessToken and RefreshToken
            PKCETokenResponse tokenResponse = new OAuthClient().RequestToken(tokenRequest).Result;
            this.Authenticator = new PKCEAuthenticator(clientId, tokenResponse);
            this.AccessToken = tokenResponse.AccessToken;
            this.RefreshToken = tokenResponse.RefreshToken;
            this.Client = new SpotifyClient(this.AccessToken);
            clientDidStart = true;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e);
            Debug.WriteLine("Failed to initialize Spotify Manager, quitting...");
            return;
        }
    }


    public HashSet<SpotifyAPI.Web.Image> GetAlbumCoversOfTopSongs()
    {
        // Raise Exception if the client has not been initialized properly
        if (!clientDidStart)
        {
            throw ClientNotInitializedException;
        }

        HashSet<SpotifyAPI.Web.Image> images = new HashSet<SpotifyAPI.Web.Image>();

        var request = Client.UserProfile.GetTopTracks(new UsersTopItemsRequest(new TimeRange()));
        UsersTopTracksResponse topTracksResponse = request.Result;
        foreach (FullTrack track in topTracksResponse.Items)
        {
            SpotifyAPI.Web.Image albumCover = track.Album.Images[0];
            images.Add(albumCover);
        }
        return images;

    }


    /// <summary>
    /// Gets the album covers for all the unique albums in your library.
    /// </summary>
    //public void GetAlbumCovers()
    //{

    //    // Throw error if client was not initialized properly.
    //    if (!clientDidStart)
    //    {
    //        throw ClientNotInitializedException;
    //    }

    //    // Get album data
    //    var albumResponse = SpotifyClient.GetAsync($"v1/me/playlists").Result;

    //    // Handle Error codes
    //    if (!albumResponse.IsSuccessStatusCode)
    //    {
    //        throw new Exception(albumResponse.Content.ReadAsStringAsync().Result);
    //    }

    //    // Get playlist data response
    //    JObject responseJson = JObject.Parse(albumResponse.Content.ReadAsStringAsync().Result);
    //    var playlistData = (JArray)responseJson["items"];

    //    // Store tracks in a list
    //    var albumCovers = new HashSet<string>();

    //    foreach (JObject album in playlistData)
    //    {
    //        // Get album cover for each track
    //        var tracks = (JObject)album["tracks"];

    //        var tracksUrl = (string)tracks["href"];

    //        var trackDataResponse = SpotifyClient.GetAsync(tracksUrl).Result;


    //        var tracksDataJson = JObject.Parse(trackDataResponse.Content.ReadAsStringAsync().Result);

    //        Debug.WriteLine(trackDataResponse.Content.ReadAsStringAsync().Result);
    //        var tracksData = (JArray)tracksDataJson["items"];

    //        foreach (JObject item in tracksData)
    //        {
    //            var trackData = (JObject)item["track"];
    //            var images = (JObject)trackData["images"];

    //        }







    //        //for ()
    //        //playlistTracks.Add(tracks);
    //    }

    //}
}
