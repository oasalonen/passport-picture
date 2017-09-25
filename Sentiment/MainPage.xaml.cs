using Microsoft.ProjectOxford.Face.Contract;
using ServiceHelpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;


namespace Sentiment
{
    public sealed partial class MainPage : Page
    {
        public class State
        {
            public MainPage Parent { get; set; }

            public Boolean IsRunning { get; set; }

            public ObservableCollection<string> Emotions { get; set; }
        }

        public State ViewModel { get; set; }

        public MainPage()
        {
            this.InitializeComponent();
            ViewModel = new State { Parent = this, Emotions = new ObservableCollection<string>() };
            cameraControl.ShowDialogOnApiErrors = true;

            this.cameraControl.ImageCaptured += CameraControl_ImageCaptured;
            this.cameraControl.CameraRestarted += CameraControl_CameraRestarted;

            Paragraph paragraph = new Paragraph();
            textDisplay.Blocks.Add(paragraph);

            Observable.FromEventPattern(textEditor, "TextChanged")
                .Throttle(TimeSpan.FromMilliseconds(1000))
                .ObserveOnDispatcher()
                .Select(_ => textEditor.Text)
                .Where(text => text.EndsWith(" "))
                .SelectMany(text =>
                {
                    textEditor.Text = "";
                    return TextAnalyticsServiceHelper.GetSentimentAsync(text)
                        .ToObservable()
                        .Select(score => Tuple.Create(text, score));
                })
                .ObserveOnDispatcher()
                .Subscribe(textAndScore =>
                {
                    var score = textAndScore.Item2;
                    Debug.WriteLine("Text: " + textAndScore.Item1);
                    Debug.WriteLine("Score: " + score);
                    TextBlock tb = new TextBlock() { Text = textAndScore.Item1 + " " };
                    Border border = new Border();
                    border.Child = tb;
                    border.Background = new SolidColorBrush(Color.FromArgb(200, (byte) (255 * (1.0 - score)), (byte) (255 * score), 0));
                    InlineUIContainer inline = new InlineUIContainer();
                    inline.Child = border;
                    paragraph.Inlines.Add(inline);
                },
                e => { Debug.WriteLine(e.ToString()); },
                () => { });

            Observable.Timer(new TimeSpan(0, 0, 0), new TimeSpan(0, 0, 1))
                .Where(i => ViewModel.IsRunning)
                .Where(i => i % 5 == 0)
                .ObserveOnDispatcher()
                .SelectMany(i =>
                {
                    Debug.WriteLine("Getting image");
                    return cameraControl.CaptureFrameAsync().ToObservable();
                })
                .Do(i => Debug.WriteLine("Got result"))
                .Where(i => i != null)
                .SelectMany(i =>
                {
                    return Task.WhenAll(i.DetectEmotionAsync(), i.DetectFacesAsync(detectFaceAttributes: true)).ToObservable().Select(_ => i);
                })
                .ObserveOnDispatcher()
                .Subscribe(image => 
                {
                    faceDescription.Text = GetFaceString(image);

                    if (image.DetectedFaces != null && image.DetectedFaces.Count() > 0 && image.DetectedFaces.First().FaceAttributes != null)
                    {
                        var attrs = image.DetectedFaces.First().FaceAttributes;
                        statusText.Text = "";
                        if (attrs.Glasses == Glasses.Sunglasses)
                        {
                            statusText.Text = "Please remove your sunglasses";
                        }
                        else if (attrs.Smile > 0.15)
                        {
                            statusText.Text = "Do not smile";
                        }
                        else if (attrs.Accessories != null && attrs.Accessories.Where(a => a.Type == AccessoryType.Headwear && a.Confidence > 0.8).Count() > 0)
                        {
                            statusText.Text = "Please remove your hat";
                        }
                        else if (attrs.HeadPose != null && Math.Abs(attrs.HeadPose.Yaw) > 6)
                        {
                            statusText.Text = "Please turn to face the camera";
                        }
                        else if (attrs.HeadPose != null && Math.Abs(attrs.HeadPose.Roll) > 6)
                        {
                            statusText.Text = "Please do not tilt your head";
                        }
                        else if (attrs.Occlusion != null && attrs.Occlusion.EyeOccluded)
                        {
                            statusText.Text = "Do not cover your eyes";
                        }
                        else if (attrs.Occlusion != null && attrs.Occlusion.ForeheadOccluded)
                        {
                            statusText.Text = "Do not cover your forehead";
                        }
                        else if (attrs.Occlusion != null && attrs.Occlusion.MouthOccluded)
                        {
                            statusText.Text = "Do not cover your mouth";
                        }
                        else if (attrs.Exposure != null && attrs.Exposure.ExposureLevel == ExposureLevel.OverExposure)
                        {
                            statusText.Text = "Your face is overexposed";
                        }
                        else if (attrs.Exposure != null && attrs.Exposure.ExposureLevel == ExposureLevel.UnderExposure)
                        {
                            statusText.Text = "Your face is underexposed";
                        }

                        if (String.IsNullOrEmpty(statusText.Text))
                        {
                            this.cameraControl.SetFaceBorderColor(new SolidColorBrush(Colors.White));
                            statusText.Foreground = new SolidColorBrush(Colors.Green);
                            statusText.Text = "Awesome passport pic!";
                        }
                        else
                        {
                            this.cameraControl.SetFaceBorderColor(new SolidColorBrush(Colors.Red));
                            statusText.Foreground = new SolidColorBrush(Colors.Red);
                        }
                    }
                },
                e => { Debug.WriteLine(e.ToString()); },
                () => { });
        }

        private string GetFaceString(ImageAnalyzer image)
        {
            try
            {
                var attrs = image.DetectedFaces.First().FaceAttributes;
                return
                    "Age: " + attrs.Age +
                    "\nGlasses: " + attrs.Glasses +
                    "\nSmile: " + attrs.Smile +
                    "\n---Accessories---" +
                    (attrs.Accessories != null ?
                    GetAccessoryText(attrs.Accessories)
                    : "\n") +
                    "\n---Facial hair---" +
                    (attrs.FacialHair != null ?
                    "\nMoustache: " + attrs.FacialHair.Moustache +
                    "\nBeard: " + attrs.FacialHair.Beard +
                    "\nSideburns: " + attrs.FacialHair.Sideburns
                    : "\n") +
                    "\n---Makeup---" +
                    (attrs.Makeup != null ? 
                    "\nEye makeup: " + attrs.Makeup.EyeMakeup +
                    "\nLipstick: " + attrs.Makeup.LipMakeup
                    : "\n") +
                    "\n---Occlusion---" +
                    (attrs.Occlusion != null ?
                    "\nForehead: " + attrs.Occlusion.ForeheadOccluded +
                    "\nEye: " + attrs.Occlusion.EyeOccluded +
                    "\nMouth: " + attrs.Occlusion.MouthOccluded
                    : "\n") +
                    "\n---Quality---" +
                    GetImageQualityText(attrs) +
                    "\n---Head pose---" +
                    (attrs.HeadPose != null ?
                    "\nYaw: " + attrs.HeadPose.Yaw +
                    "\nPitch: " + attrs.HeadPose.Pitch +
                    "\nRoll: " + attrs.HeadPose.Roll
                    : "\n") +
                    "\n---Emotion---" +
                    (attrs.Emotion != null ?
                    "\nNeutral: " + attrs.Emotion.Neutral +
                    "\nHappiness: " + attrs.Emotion.Happiness +
                    "\nAnger: " + attrs.Emotion.Anger +
                    "\nContempt: " + attrs.Emotion.Contempt +
                    "\nDisgust: " + attrs.Emotion.Disgust +
                    "\nFear: " + attrs.Emotion.Fear +
                    "\nSadness: " + attrs.Emotion.Sadness +
                    "\nSurprise: " + attrs.Emotion.Surprise
                    : "\n");
            }
            catch
            {
                return "";
            }
        }

        private string GetAccessoryText(Accessory[] accessories)
        {
            return accessories
                .Select(accessory => "\n" + accessory.Type.ToString() + ": " + accessory.Confidence)
                .Aggregate("", (list, acc) => list + acc);
        }

        private string GetImageQualityText(FaceAttributes attributes)
        {
            var noise = attributes.Noise != null ? "\nNoise: " + attributes.Noise.NoiseLevel.ToString() + " (" + attributes.Noise.Value + ")" : "";
            var exposure = attributes.Exposure != null ? "\nExposure: " + attributes.Exposure.ExposureLevel.ToString() + " (" + attributes.Exposure.Value + ")" : "";
            var blur = attributes.Blur != null ? "\nBlur: " + attributes.Blur.BlurLevel.ToString() + " (" + attributes.Blur.Value + ")" : "";
            return noise + exposure + blur;
        }

        private async void CameraControl_CameraRestarted(object sender, EventArgs e)
        {
            // We induce a delay here to give the camera some time to start rendering before we hide the last captured photo.
            // This avoids a black flash.
            await Task.Delay(500);
        }

        private async void CameraControl_ImageCaptured(object sender, ImageAnalyzer e)
        {
            await this.cameraControl.StopStreamAsync();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            await this.cameraControl.StopStreamAsync();
            base.OnNavigatingFrom(e);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await this.StartWebCameraAsync();
            base.OnNavigatedTo(e);
        }

        private async void OnImageSearchCompleted(object sender, IEnumerable<ImageAnalyzer> args)
        {
            //this.imageSearchFlyout.Hide();
            ImageAnalyzer image = args.First();
            image.ShowDialogOnFaceApiErrors = true;

            //this.imageWithFacesControl.Visibility = Visibility.Visible;
            //this.webCamHostGrid.Visibility = Visibility.Collapsed;
            await this.cameraControl.StopStreamAsync();

            //this.imageWithFacesControl.DataContext = image;
        }

        private void OnImageSearchCanceled(object sender, EventArgs e)
        {
            //this.imageSearchFlyout.Hide();
        }

        private async void OnWebCamButtonClicked(object sender, RoutedEventArgs e)
        {
            await StartWebCameraAsync();
        }

        private async Task StartWebCameraAsync()
        {
            webCamHostGrid.Visibility = Visibility.Visible;
            //imageWithFacesControl.Visibility = Visibility.Collapsed;

            await this.cameraControl.StartStreamAsync();
            await Task.Delay(250);
            //this.imageFromCameraWithFaces.Visibility = Visibility.Collapsed;

            UpdateWebCamHostGridSize();
        }

        private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            //UpdateWebCamHostGridSize();
        }

        private void UpdateWebCamHostGridSize()
        {
            this.webCamHostGrid.Width = Math.Min(this.webCamHostGrid.ActualHeight * (this.cameraControl.CameraAspectRatio != 0 ? this.cameraControl.CameraAspectRatio : 1.777777777777), this.Width);
        }

        private void Debug_Click(object sender, RoutedEventArgs e)
        {
            if (faceDescriptionContainer.Visibility == Visibility.Visible)
            {
                faceDescriptionContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                faceDescriptionContainer.Visibility = Visibility.Visible;
            }
        }
    }
}
