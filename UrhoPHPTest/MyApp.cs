﻿using System;
using System.Diagnostics;
using Urho;
using Urho.Actions;
using Urho.Gui;
using Urho.Shapes;

namespace UrhoPHPTest
{
    public class MyApp : Application
    {
        Text helloText;
        Camera camera;
        Node cameraNode;
        Node earthNode;
        Node rootNode;
        Node cloudsNode;
        Scene scene;
        Viewport viewport;
        Skybox skybox;
        float yaw, pitch;

        App app;

        //An utility function making direct references from C# project to the classes created in PHP.
        private void CreatePHPReferences()
        {
            helloText = ((Text)app.helloText.AsObject());
            scene = ((Scene)app.scene.AsObject());
            rootNode = ((Node)app.rootNode.AsObject());
            earthNode = ((Node)app.earthNode.AsObject());
            cloudsNode = ((Node)app.cloudsNode.AsObject());
            viewport = ((Viewport)app.viewport.AsObject());
            camera = ((Camera)app.camera.AsObject());
            cameraNode = ((Node)app.cameraNode.AsObject());
            skybox = ((Skybox)app.skybox.AsObject());
        }

        [Preserve]
        public MyApp(ApplicationOptions options) : base(options) { }

        static MyApp()
        {
            UnhandledException += (s, e) =>
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                e.Handled = true;
            };
        }

        protected override async void Start()
        {
            base.Start();

            //creating empty PHP context for utility use
            var ctx = Pchp.Core.Context.CreateEmpty();
           
            //The PHP app class containing all the application 
            app = new App(ctx, "Hey from C#");

            app.Start();

            // Earth and Moon
            app.createEarthTexture();
            app.createMoon();

            //Clouds
            var cloudsMaterial = new Material();
            cloudsMaterial.SetTexture(TextureUnit.Diffuse, ResourceCache.GetTexture2D("Textures/Earth_Clouds.jpg"));
            cloudsMaterial.SetTechnique(0, CoreAssets.Techniques.DiffAddAlpha);

            app.createClouds(cloudsMaterial);

            // Light
            app.createLight();

            app.createCameraAndView();

            //Material skyboxMaterial = Material.SkyboxFromImage("Textures/Space.png");
            //app.createSkybox(skyboxMaterial);
                
            //Create the references to objects created in PHP inside MyApp UrhoSharp Application
            CreatePHPReferences();

            // Text created in php proj updated here
            helloText.SetColor(new Color(0.5f, 1.0f, 1.0f, 1.0f));
            helloText.SetFont(font: CoreAssets.Fonts.AnonymousPro, size: 30);

            // Necessary to call from here because of UI belonging to extended Application class
            UI.Root.AddChild(helloText);

            //// Camera
            //cameraNode = scene.CreateChild();
            //var camera = cameraNode.CreateComponent<Camera>();

            //// Viewport
            var viewport = new Viewport(scene, camera, null);
            Renderer.SetViewport(0, viewport);
            ////viewport.RenderPath.Append(CoreAssets.PostProcess.FXAA2);
            
            // Setting Application properties
            Input.Enabled = true;
            // FPS
            new MonoDebugHud(this).Show(Color.Green, 25);

            // Stars (Skybox)
            var skyboxNode = scene.CreateChild();
            var skybox = skyboxNode.CreateComponent<Skybox>();
            skybox.Model = CoreAssets.Models.Box;
            skybox.SetMaterial(Material.SkyboxFromImage("Textures/Space.png"));

            // Run a an action to spin the Earth (7 degrees per second)
            app.runRotations(-7, 1);

            await rootNode.RunActionsAsync(new EaseOut(new MoveTo(2f, new Vector3(0, 0, 12)), 1));

            AddCity(0, 0, "(0, 0)");
            AddCity(53.9045f, 27.5615f, "Minsk");
            AddCity(51.5074f, 0.1278f, "London");
            AddCity(40.7128f, -74.0059f, "New-York");
            AddCity(37.7749f, -122.4194f, "San Francisco");
            AddCity(39.9042f, 116.4074f, "Beijing");
            AddCity(-31.9505f, 115.8605f, "Perth");
        }
        public void AddCity(float lat, float lon, string name)
        {
            var height = earthNode.Scale.Y / 2f;

            lat = (float)Math.PI * lat / 180f - (float)Math.PI / 2f;
            lon = (float)Math.PI * lon / 180f;

            float x = height * (float)Math.Sin(lat) * (float)Math.Cos(lon);
            float z = height * (float)Math.Sin(lat) * (float)Math.Sin(lon);
            float y = height * (float)Math.Cos(lat);

            var markerNode = rootNode.CreateChild();
            markerNode.Scale = Vector3.One * 0.1f;
            markerNode.Position = new Vector3((float)x, (float)y, (float)z);
            markerNode.CreateComponent<Sphere>();
            markerNode.RunActionsAsync(new RepeatForever(
                new TintTo(0.5f, Color.White),
                new TintTo(0.5f, Randoms.NextColor())));

            var textPos = markerNode.Position;
            textPos.Normalize();

            var textNode = markerNode.CreateChild();
            textNode.Position = textPos * 2;
            textNode.SetScale(3f);
            textNode.LookAt(Vector3.Zero, Vector3.Up, TransformSpace.Parent);
            var text = textNode.CreateComponent<Text3D>();
            text.SetFont(CoreAssets.Fonts.AnonymousPro, 150);
            text.EffectColor = Color.Black;
            text.TextEffect = TextEffect.Shadow;
            text.Text = name;
        }

        protected override void OnUpdate(float timeStep)
        {
            MoveCameraByTouches(timeStep);
            SimpleMoveCamera3D(timeStep);
            base.OnUpdate(timeStep);
        }

        /// <summary>
        /// Move camera for 3D samples
        /// </summary>
        protected void SimpleMoveCamera3D(float timeStep, float moveSpeed = 10.0f)
        {
            if (!Input.GetMouseButtonDown(MouseButton.Left))
                return;

            const float mouseSensitivity = .1f;
            var mouseMove = Input.MouseMove;
            yaw += mouseSensitivity * mouseMove.X;
            pitch += mouseSensitivity * mouseMove.Y;
            pitch = MathHelper.Clamp(pitch, -90, 90);
            cameraNode.Rotation = new Quaternion(pitch, yaw, 0);
        }

        protected void MoveCameraByTouches(float timeStep)
        {
            const float touchSensitivity = 2f;

            var input = Input;
            for (uint i = 0, num = input.NumTouches; i < num; ++i)
            {
                TouchState state = input.GetTouch(i);
                if (state.Delta.X != 0 || state.Delta.Y != 0)
                {
                    var camera = cameraNode.GetComponent<Camera>();
                    if (camera == null)
                        return;

                    yaw += touchSensitivity * camera.Fov / Graphics.Height * state.Delta.X;
                    pitch += touchSensitivity * camera.Fov / Graphics.Height * state.Delta.Y;
                    cameraNode.Rotation = new Quaternion(pitch, yaw, 0);
                }
            }
        }
    }
}
