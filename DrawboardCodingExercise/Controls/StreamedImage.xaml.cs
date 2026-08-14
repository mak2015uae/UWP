using System;
using System.IO;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace DrawboardCodingExercise.Controls;

/// <summary>
/// Displays an image supplied as a stream, showing progress while it decodes and an error glyph if it cannot.
/// </summary>
/// <remarks>
/// Takes a stream rather than a URI so that images fetched through the application's own HTTP client — and
/// therefore its logging, correlation and error handling — can be displayed without the XAML image element
/// making its own unmanaged request.
/// <para>
/// Unused by the current film-based implementation, which has no imagery, and retained for an API whose payloads
/// include thumbnails.
/// </para>
/// </remarks>
public sealed partial class StreamedImage
{
	/// <summary>
	/// Identifies the <see cref="SourceStream"/> dependency property.
	/// </summary>
	public static readonly DependencyProperty SourceStreamProperty = DependencyProperty.Register(
		nameof(SourceStream), typeof(Stream), typeof(StreamedImage), new PropertyMetadata(default(Stream), ImageStreamChanged));

	/// <summary>
	/// Identifies the <see cref="ErrorBrush"/> dependency property.
	/// </summary>
	public static readonly DependencyProperty ErrorBrushProperty = DependencyProperty.Register(
		nameof(ErrorBrush), typeof(Brush), typeof(StreamedImage), new PropertyMetadata(new SolidColorBrush(Colors.Red)));

	/// <summary>
	/// Gets or sets the brush used to draw the error glyph shown when a stream cannot be decoded.
	/// </summary>
	public Brush ErrorBrush
	{
		get => (Brush) GetValue(ErrorBrushProperty);
		set => SetValue(ErrorBrushProperty, value);
	}

	/// <summary>
	/// Begins decoding whenever a new stream is assigned.
	/// </summary>
	/// <param name="d">The control whose property changed.</param>
	/// <param name="e">The change details, carrying the new stream.</param>
	private static void ImageStreamChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is StreamedImage streamedImage) streamedImage.LoadFromStreamAsync((Stream)e.NewValue);
	}

	/// <summary>
	/// Decodes a stream into the presented image, reporting progress and failure through the control's own visuals.
	/// </summary>
	/// <param name="stream">The image stream to decode.</param>
	/// <remarks>
	/// Deliberately <c>async void</c>: it is invoked from a dependency property change callback, which cannot
	/// await. Every failure is therefore handled here rather than escaping as an unobserved exception.
	/// </remarks>
	private async void LoadFromStreamAsync(Stream stream)
	{
		ProgressRing.IsActive = true;
		PresentedImage.Visibility = Visibility.Collapsed;
		ErrorIcon.Visibility = Visibility.Collapsed;
		try
		{
			BitmapImage image = new BitmapImage();

			await image.SetSourceAsync(stream.AsRandomAccessStream());

			PresentedImage.Source = image;

			PresentedImage.Visibility = Visibility.Visible;
		}
		catch (Exception)
		{
			PresentedImage.Visibility = Visibility.Collapsed;
			ErrorIcon.Visibility = Visibility.Visible;
		}
		finally
		{
			ProgressRing.IsActive = false;
		}
	}

	/// <summary>
	/// Gets or sets the stream containing the image to display.
	/// </summary>
	/// <value>
	/// The image data. Assigning a new value restarts decoding. The control reads the stream but does not take
	/// ownership of it, so the supplier remains responsible for disposal.
	/// </value>
	public Stream SourceStream
	{
		get => (Stream) GetValue(SourceStreamProperty);
		set => SetValue(SourceStreamProperty, value);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="StreamedImage"/> class.
	/// </summary>
	public StreamedImage()
	{
		this.InitializeComponent();
	}
}
