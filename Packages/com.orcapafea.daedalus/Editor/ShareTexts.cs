using System.Globalization;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Every message Share my game shows, in short plain English that names no code. The Korean words are the class
    /// gallery's own button labels, copied exactly from Deploy/gallery/src/strings.rs so a student can match what they
    /// read here with what they see there; they must stay identical (Tools/share_check.py checks).
    /// </summary>
    internal static class ShareTexts
    {
        /// <summary>The class gallery (one gallery serves every class; Deploy/gallery/src/config.rs <c>GALLERY_HOST</c>).</summary>
        internal const string GalleryAddress = "https://suwon.orcarin.com";

        /// <summary>strings.rs <c>NAV_UPLOAD</c>: the upload link every signed-in student sees.</summary>
        internal const string GalleryUpload = "업로드";
        /// <summary>strings.rs <c>UPDATE_MY_GAME</c>.</summary>
        internal const string GalleryUpdateMyGame = "내 게임 업데이트하기";
        /// <summary>strings.rs <c>NEW_GAME</c>.</summary>
        internal const string GalleryNewGame = "새 게임";

        internal const string Title = "Share my game";
        internal const string PlayMode = "Stop Play mode first, then choose Share my game again.";
        internal const string Compiling = "Unity is still getting ready. Wait until it finishes, then try again.";
        internal const string NoWebModule = "This Unity can't make web games yet. In Unity Hub, open Installs, pick this Unity version, choose Add modules and tick WebGL Build Support. Then restart Unity.";
        internal const string Untitled = "Save your scene first, then share it.";
        internal const string NoPlayer = "This scene has no Player, so the game would be empty. Add a Player (Add Component → Daedalus → Player) and share again.";
        internal const string NoMainCamera = "This scene has no Main Camera, so the game can't follow the player and the gallery has no picture for it. Tag a camera MainCamera and share again.";
        internal const string SaveFailed = "Unity couldn't save your scene, so nothing was shared. Check that the scene file isn't read-only, then try again.";
        internal const string Unexpected = "Unity couldn't finish the share, so no zip was made. If the zip is open in another program, close it or pick another folder, then share again. If it keeps happening, show your teacher the Console.";
        internal const string EmptyTitle = "Type a title for your game.";
        internal const string SceneMissing = "The scene {0} can't be found. Open the scene you want to share and try again.";
        internal const string NoPicture = "Unity couldn't take the picture for your game's card, so no zip was made. Share again; if it happens again, show your teacher the Console.";
        internal const string BuildFailed = "The build didn't finish, so no zip was made. Open the Console (Window → General → Console) and look for a red message about your scene or scripts. Fix it and share again, or show your teacher.";
        internal const string BuildStopped = "The build was stopped, so no zip was made.";
        internal const string BuildException = "Something went wrong while Unity prepared your game, so it might show nothing. No zip was made. Open the Console to see what happened, then share again.";
        internal const string TooBig = "Your game is {0} MB. The class gallery takes up to 50 MB. Use fewer or smaller pictures and sounds, then share again.";
        internal const string Done = "Your game is ready: {0}\n\nNext: open the class gallery, sign in, choose " + GalleryUpload + " and pick this file as " + GalleryNewGame + ".\n\nAlready shared this game? Choose " + GalleryUpdateMyGame + " instead, so players keep their progress.";
        internal const string WindowNote = "Your scene is saved first. The first share takes several minutes and Unity will look busy. Don't close Unity.";
        internal const string GalleryWillShow = "The gallery will show: {0}";
        internal const string Scene = "Scene: {0}";
        internal const string ShowZip = "Show the zip";
        internal const string OpenGallery = "Open the gallery";
        internal const string Close = "Close";

        /// <summary>The too-big message: megabytes with one decimal, rounded up, so a zip just over the limit never reads "50 MB".</summary>
        internal static string TooBigFor(long bytes)
        {
            double megabytes = System.Math.Ceiling(bytes / 100000.0) / 10.0;
            return string.Format(CultureInfo.InvariantCulture, TooBig, megabytes.ToString("0.0", CultureInfo.InvariantCulture));
        }
    }
}
