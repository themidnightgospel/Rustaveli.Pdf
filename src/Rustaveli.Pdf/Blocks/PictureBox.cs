namespace Rustaveli.Pdf.Blocks;

/// <summary>
/// The box a picture — an image or artwork — is drawn in, given the room it is offered and how it is fitted to it.
/// </summary>
internal static class PictureBox
{
    /// <param name="fitting">How the picture is fitted; a value this library does not know fits the width.</param>
    /// <param name="aspect">The picture's width divided by its height.</param>
    /// <param name="room">The room the picture is offered.</param>
    public static Extent Of(ImageFitting fitting, float aspect, Extent room)
    {
        Extent fullWidth = new Extent(room.Width, room.Width / aspect);

        switch (fitting)
        {
            case ImageFitting.Stretch:
                return room;
            case ImageFitting.FitHeight:
                return new Extent(room.Height * aspect, room.Height);
            case ImageFitting.Proportionally:
                // As large as fits whole: the full width, unless that makes it taller than the room.
                return fullWidth.Height <= room.Height + Extent.Epsilon
                    ? fullWidth
                    : new Extent(room.Height * aspect, room.Height);
            default:
                return fullWidth;
        }
    }
}
