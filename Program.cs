using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.ObjectAnchors;
using Microsoft.Azure.ObjectAnchors.SpatialGraph;
using Windows.Perception.Spatial;
using Windows.Perception.Spatial.Preview;

namespace StereoKit.Samples.AzureObjectAnchors
{
    class Program
    {
        /// <summary>
        /// This demo was built using the Azure Object Anchors SDK documentation available on <see cref="https://docs.microsoft.com/en-us/azure/object-anchors/concepts/sdk-overview"/>.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            // Initialize StereoKit
            SKSettings settings = new SKSettings
            {
                appName = "StereoKit.Samples.AzureObjectAnchors",
                assetsFolder = "Assets",
            };
            if (!SK.Initialize(settings))
                Environment.Exit(1);



            if (!ObjectObserver.IsSupported())
            {
                Log.Err("Azure Object Anchors not supported!");
                Environment.Exit(1);
            }

            // This call should grant the access we need.
            //ObjectObserverAccessStatus status = await ObjectObserver.RequestAccessAsync();
            //if (status != ObjectObserverAccessStatus.Allowed)
            //{
            //    Log.Err("Azure Object Anchors access not allowed!");
            //    Environment.Exit(1);
            //}

            // Note that you need to provide the Id, Key and Domain for your Azure Object
            // Anchors account.
            Guid accountId = new Guid("[your account id]");
            string accountKey = "[your account key]";
            string accountDomain = "[your account domain]";

            AccountInformation accountInformation = new AccountInformation(accountId, accountKey, accountDomain);
            ObjectAnchorsSession session = new ObjectAnchorsSession(accountInformation);
            ObjectObserver observer = session.CreateObjectObserver();


            


            SK.Run(() => {

                UI.PanelBegin();
                if(UI.Button("Load 3D Model"))
                {

                    // Load a model into a byte array. The model could be a file, an embedded
                    // resource, or a network stream.
                    byte[] modelAsBytes;
                    Platform.FilePicker(PickerMode.Open, async file => {
                        // On some platforms (like UWP), you may encounter permission
                        // issues when trying to read or write to an arbitrary file.
                        //
                        // StereoKit's `Platform.FilePicker` and `Platform.ReadFile`
                        // work together to avoid this permission issue, where the
                        // FilePicker will grant permission to the ReadFile method.
                        // C#'s built-in `File.ReadAllText` would fail on UWP here.
                        if (Platform.ReadFile(file, out modelAsBytes))
                        {
                            ObjectModel model = await observer.LoadObjectModelAsync(modelAsBytes);
                            // Note that after a model is loaded, its vertices and normals are transformed
                            // into a centered coordinate system for the ease of computing the object pose.
                            // The rigid transform can be retrieved through the `OriginToCenterTransform`
                            // property.

                            //Gets the SpatialLocator instance that tracks the location of the current device, such as a HoloLens, relative to the user's surroundings.
                            SpatialLocator locator = SpatialLocator.GetDefault();
                            //Creates a frame of reference that remains stationary relative to the user's surroundings, with its initial origin at the SpatialLocator's current location.
                            SpatialStationaryFrameOfReference stationary = locator.CreateStationaryFrameOfReferenceAtCurrentLocation();
                            // Creates an anchor at the origin of the specified coordinate system.

                            SpatialGraphCoordinateSystem coordinateSystem = new SpatialGraphCoordinateSystem();
                            SpatialGraphInteropFrameOfReferencePreview frameOfReference = SpatialGraphInteropPreview.TryCreateFrameOfReference(stationary.CoordinateSystem);
                            if (frameOfReference != null)
                            {
                                coordinateSystem.NodeId = frameOfReference.NodeId;
                                coordinateSystem.CoordinateSystemToNodeTransform = frameOfReference.CoordinateSystemToNodeTransform;
                            }

                            // Get the search area.
                            SpatialFieldOfView fieldOfView = new SpatialFieldOfView
                            {
                                Position = Renderer.CameraRoot.Pose.position,
                                Orientation = Renderer.CameraRoot.Pose.orientation,
                                FarDistance = 4.0f, // Far distance in meters of object search frustum.
                                HorizontalFieldOfViewInDegrees = 75.0f, // Horizontal field of view in
                                                                        // degrees of object search frustum.
                                AspectRatio = 1.0f // Aspect ratio (horizontal / vertical) of object search
                                                   // frustum.
                            };

                            ObjectSearchArea searchArea = ObjectSearchArea.FromFieldOfView(coordinateSystem, fieldOfView);

                            // Optionally change the parameters, otherwise use the default values embedded
                            // in the model.
                            ObjectQuery query = new ObjectQuery(model);
                            query.MinSurfaceCoverage = 0.2f;
                            query.ExpectedMaxVerticalOrientationInDegrees = 1.5f;
                            query.MaxScaleChange = 0.1f;
                            query.SearchAreas.Add(searchArea);

                            // Detection could take a while, so we run it in a background thread.
                            IList<ObjectInstance> detectedObjects = await observer.DetectAsync(query);

                            foreach (ObjectInstance instance in detectedObjects)
                            {
                                // Supported modes:
                                // "LowLatencyCoarsePosition"    - Consumes less CPU cycles thus fast to
                                //                                 update the state.
                                // "HighLatencyAccuratePosition" - (Not yet implemented) Consumes more CPU
                                //                                 cycles thus potentially taking longer
                                //                                 time to update the state.
                                // "Paused"                      - Stops to update the state until mode
                                //                                 changed to low or high.
                                instance.Mode = ObjectInstanceTrackingMode.LowLatencyCoarsePosition;


                                // Listen to state changed event on this instance.
                                instance.Changed += Instance_Changed;
                            }
                        }
                    }, null, ".gltf", ".obj", ".fbx");
                }
            
            
            
            
            }, ()=> { });

            SK.Shutdown();
        }

        private static void Instance_Changed(object sender, ObjectInstanceChangedEventArgs args)
        {
            // Try to query the current instance state.
            ObjectInstanceState? state = (sender as ObjectInstance)?.TryGetCurrentState();

            if (state.HasValue)
            {
                // Process latest state via state.Value.
                // An object pose includes scale, rotation and translation, applied in
                // the same order to the object model in the centered coordinate system.
            }
            else
            {
                // This object instance is lost for tracking, and will never be recovered.
                // The caller can detach the Changed event handler from this instance
                // and dispose it.
                (sender as ObjectInstance)?.Dispose();
            }
        }
    }
}
