using System;
using System.Configuration;
using System.Collections.Specialized;
namespace SpotifyScreenSaver
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            string clientId = ConfigurationManager.AppSettings.Get("SPOTIFY_CLIENT_ID");

           
            SpotifyManager spotify = new SpotifyManager(clientId ?? "");

            Application.Run(new Form1());
        }
    }
}