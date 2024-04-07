using System.Configuration;
using System.Diagnostics;

namespace SpotifyScreenSaver;
public partial class ScreenSaverForm : Form
{
    public ScreenSaverForm(Rectangle bounds)
    {
        InitializeComponent();
        this.Bounds = bounds;
    }

    private void ScreenSaverForm_Load(object sender, EventArgs e)
    {
        Debug.WriteLine("Loading...");

        // Initialize variables
        int ROWS = 4;
        int COLS = 5;
        int SIDE_LENGTH = Bounds.Size.Width / COLS;

        // Get client Id
        string? clientId = ConfigurationManager.AppSettings.Get("SPOTIFY_CLIENT_ID");
        string? clientSecret = ConfigurationManager.AppSettings.Get("SPOTIFY_CLIENT_SECRET");
        SpotifyManager spotify = new SpotifyManager(clientId ?? "", clientSecret ?? "");

        // Get top songs
        SpotifyAPI.Web.Image[] imagesArray = spotify.GetAlbumCoversOfTopSongs().ToArray();

        // Calculate the total number of PictureBoxes needed
        int totalPictureBoxes = Math.Min(imagesArray.Length, ROWS * COLS);

        for (int i = 0; i < totalPictureBoxes; i++)
        {
            // Format Location, Size, backColor, and SizeMode
            PictureBox pb = new PictureBox();
            pb.Location = new Point((i % COLS) * SIDE_LENGTH, (i / COLS) * SIDE_LENGTH);
            pb.Size = new Size(SIDE_LENGTH, SIDE_LENGTH);
            pb.BackColor = Color.White;
            pb.SizeMode = PictureBoxSizeMode.StretchImage;

            // Load image asynchronously
            pb.LoadAsync(imagesArray[i].Url);

            // Add PictureBox to the form's Controls collection
            Controls.Add(pb);
            pb.MouseMove += ScreenSaverForm_MouseMove;
            pb.Show();
        }
    }


    private Point previousMouseLocation;
    private void ScreenSaverForm_MouseMove(object sender, MouseEventArgs e)
    {
        if (previousMouseLocation != Point.Empty)
        {
            if (Math.Abs(previousMouseLocation.X - e.X) > 5 ||
                Math.Abs(previousMouseLocation.Y - e.Y) > 5)
            {
                Application.Exit();
                return;
            }
        }

        // Update previous mouse location
        previousMouseLocation = e.Location;
    }

    private void ScreenSaverForm_MouseClick(object sender, EventArgs e)
    {
        Debug.WriteLine("Oh Wow");
        Application.Exit();

    }


}
