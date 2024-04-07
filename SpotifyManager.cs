using SpotifyAPI.Web;
using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
public class SpotifyManager
{

    // ######
    // FIELDS
    // ######

    // Client id for API application
    private string ClientId = "";

    private string ACCESS_TOKEN_KEY = "SSS_ACCESS_TOKEN";
    private string REFRESH_TOKEN_KEY = "SSS_REFRESH_TOKEN";

    // Access Token to use API
    private string AccessToken = "";

    // Refresh Token
    private string RefreshToken = "";

    // HttpListener to act as server
    private HttpListener Server = new HttpListener();

    // SpotifyClient for making API calls
    private SpotifyClient Client;

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
    /// Gets the code to supply when requesting an access token.
    /// </summary>
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

    private AuthorizationCodeTokenResponse GetInitialTokenResponse(string clientId, string clientSecret)
    {
        // Variable initalization
        string REDIRECT_URI_PATH = "http://localhost:8080/";
        string SPOTIFY_AUTH_PATH = "https://accounts.spotify.com";
        string SPOTIFY_API_PATH = "https://api.spotify.com";
        // Initialize listener
        Server.Prefixes.Add(REDIRECT_URI_PATH.ToString());
        Server.Start();
        // Create login request
        LoginRequest loginRequest = new LoginRequest(
            new Uri(REDIRECT_URI_PATH + "callback"), clientId, LoginRequest.ResponseType.Code
)
        {
            Scope = new[]  { Scopes.PlaylistReadPrivate, Scopes.PlaylistReadCollaborative,
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
        AuthorizationCodeTokenRequest tokenRequest = new AuthorizationCodeTokenRequest(clientId, clientSecret, authCode, new Uri(REDIRECT_URI_PATH + "callback"));

        // Return response
        var tokenResponse = new OAuthClient().RequestToken(tokenRequest).Result;
        return tokenResponse;
    }

    /// <summary>
    /// Instantiates a SpotifyManager, which handles Spotify API requests on behalf of the client.
    /// </summary>
    /// <param name="clientId">The client id of the Spotify application used when making API requests.</param>
    public SpotifyManager(string clientId, string clientSecret)
    {
        EnvironmentVariableTarget TARGET = EnvironmentVariableTarget.User;
        // Fail if client id is null
        if (string.IsNullOrEmpty(clientId))
        {
            Exception NullClientIdError = new Exception("Client Id is empty.");
            throw NullClientIdError;
        }

        try
        {
            // Generate Access Token/ Refresh Token for application
            AuthorizationCodeTokenResponse initialResponse = GetInitialTokenResponse(clientId, clientSecret);

            // Start the SpotifyClient
            SpotifyClientConfig config = SpotifyClientConfig.CreateDefault().WithAuthenticator(new AuthorizationCodeAuthenticator(clientId, clientSecret, initialResponse));
            this.Client = new SpotifyClient(config);
            this.clientDidStart = true;
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
        if (!this.clientDidStart)
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
