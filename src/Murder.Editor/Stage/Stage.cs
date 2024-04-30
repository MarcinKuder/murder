﻿using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Murder.Core;
using Murder.Core.Geometry;
using Murder.Core.Graphics;
using Murder.Editor.Assets;
using Murder.Editor.Components;
using Murder.Editor.ImGuiExtended;
using Murder.Editor.Utilities;
using Murder.Services;
using Murder.Utilities;
using System.Numerics;

namespace Murder.Editor.Stages
{
    /// <summary>
    /// Base implementation for rendering the world in the screen.
    /// </summary>
    public partial class Stage : IDisposable
    {
        protected readonly MonoWorld _world;

        private readonly RenderContext _renderContext;
        private readonly ImGuiRenderer _imGuiRenderer;

        private bool _calledStart = false;

        /// <summary>
        /// Texture used by ImGui when printing in the screen.
        /// </summary>
        private IntPtr _imGuiRenderTexturePtr;

        public readonly EditorHook EditorHook;

        public bool ShowInfo { get; set; } = true;
        public string? FocusOnGroup = null;

        public Stage(ImGuiRenderer imGuiRenderer, RenderContext renderContext, Guid? worldGuid = null) :
            this(imGuiRenderer, renderContext, hook: new(), worldGuid) { }

        public Stage(ImGuiRenderer imGuiRenderer, RenderContext renderContext, EditorHook hook, Guid? worldGuid = null)
        {
            _imGuiRenderer = imGuiRenderer;
            _renderContext = renderContext;

            _world = new MonoWorld(StageHelpers.FetchEditorSystems(), _renderContext.Camera, worldGuid ?? Guid.Empty);

            EditorComponent editorComponent = new(hook);

            EditorHook = editorComponent.EditorHook;

            EditorHook.ShowDebug = true;
            EditorHook.GetEntityIdForGuid = GetEntityIdForGuid;
            EditorHook.GetNameForEntityId = GetNameForEntityId;
            EditorHook.EnableSelectChildren = worldGuid is null;

            if (worldGuid is not null &&
                Architect.EditorSettings.CameraPositions.TryGetValue(worldGuid.Value, out PersistStageInfo info))
            {
                EditorHook.CurrentZoomLevel = info.Zoom;
            }

            _world.AddEntity(editorComponent);
        }
        
        private void InitializeDrawAndWorld()
        {
            _calledStart = true;

            if (_renderContext.LastRenderTarget is RenderTarget2D target)
            {
                _imGuiRenderTexturePtr = _imGuiRenderer.BindTexture(target);
            }

            _world.Start();
        }

        public void Update()
        {
            if (!_calledStart)
            {
                InitializeDrawAndWorld();
            }

            // Only update the stage if it's active.
            if (Game.Instance.IsActive)
            {
                _world.Update();
            }

            if (Game.NowUnscaled >= _targetFixedUpdateTime)
            {
                _world.FixedUpdate();
                _targetFixedUpdateTime = Game.NowUnscaled + Game.FixedDeltaTime;
            }
        }

        public void Draw(Rectangle? rectToDrawStage = null)
        {
            if (!_calledStart)
            {
                InitializeDrawAndWorld();
            }

            ImGui.InvisibleButton("map_canvas", ImGui.GetContentRegionAvail() - new Vector2(0, 5));
            if (ImGui.IsItemHovered())
            {
                EditorHook.UsingGui = false;
            }
            else
            {
                EditorHook.UsingGui = true;
            }

            Vector2 topLeft = rectToDrawStage?.TopLeft ?? ImGui.GetItemRectMin();
            Vector2 bottomRight = rectToDrawStage?.BottomRight ?? ImGui.GetItemRectMax();

            Vector2 size = rectToDrawStage?.Size ?? Rectangle.FromCoordinates(topLeft, bottomRight).Size;

            if (size.X <= 0 || size.Y <= 0)
            {
                // Empty.
                return;
            }

            float maxAxis = Math.Max(size.X, size.Y);
            Vector2 ratio = size.ToCore() / maxAxis;
            int maxSize = Calculator.RoundToInt(maxAxis);

            var cameraSize = new Point(Calculator.RoundToEven(ratio.X * maxSize), Calculator.RoundToEven(ratio.Y * maxSize));

            if (cameraSize != _renderContext.Camera.Size)
            {
                Point diff = _renderContext.Camera.Size - cameraSize;

                // TODO : Implement DPI
                // var dpi = ImGui.GetIO().FontGlobalScale;

                if (_renderContext.RefreshWindow(Architect.GraphicsDevice, cameraSize, cameraSize, new ViewportResizeStyle(ViewportResizeMode.None)))
                {
                    if (_imGuiRenderTexturePtr == 0) // Not initialized yet
                    {
                        _imGuiRenderTexturePtr = _imGuiRenderer.BindTexture(_renderContext.LastRenderTarget!);
                    }
                    else // Just resizing the screen
                    {
                        _imGuiRenderer.BindTexture(_imGuiRenderTexturePtr, _renderContext.LastRenderTarget!, false);
                    }
                    _renderContext.Camera.Position += diff / 2;
                }
            }

            if (_world.GetUnique<EditorComponent>() is EditorComponent editorComponent)
            {
                editorComponent.EditorHook.Offset = topLeft.Point();
                editorComponent.EditorHook.StageSize = rectToDrawStage?.Size ?? 
                    Rectangle.FromCoordinates(topLeft, bottomRight).Size;
            }

            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            drawList.PushClipRect(topLeft, bottomRight);

            DrawWorld();

            _imGuiRenderer.BindTexture(_imGuiRenderTexturePtr, _renderContext.LastRenderTarget!, unloadPrevious: false);
            drawList.AddImage(_imGuiRenderTexturePtr, topLeft, bottomRight);

            if (ShowInfo)
            {
                // Add useful coordinates
                var cursorWorld = EditorHook.CursorWorldPosition;
                var cursorScreen = EditorHook.CursorScreenPosition;

                DrawTextRoundedRect(drawList, new Vector2(10, 10) + topLeft,
                    Game.Profile.Theme.Bg, Game.Profile.Theme.Accent, (cursorWorld!=null?
                    $"Cursor: {cursorWorld.Value.X}, {cursorWorld.Value.Y}": "Not Available"));

                float yText = 20;
                if (!EditorHook.SelectionBox.IsEmpty)
                {
                    DrawTextRoundedRect(drawList, new Vector2(10, 10 + yText) + topLeft,
                        Game.Profile.Theme.Bg, Game.Profile.Theme.Accent,
                        $"Rect: {EditorHook.SelectionBox.X:0.##}, {EditorHook.SelectionBox.Y:0.##}, {EditorHook.SelectionBox.Width:0.##}, {EditorHook.SelectionBox.Height:0.##}");
                    yText += 20;
                }

                DrawTextRoundedRect(drawList, new Vector2(10, 10 + yText) + topLeft,
                    Game.Profile.Theme.Bg, Game.Profile.Theme.Accent,
                    $"Camera: {_world.Camera.Position.X:0.000}, {_world.Camera.Position.Y:0.000} (z: {_world.Camera.Zoom})");
            }

            drawList.PopClipRect();

            if (_world.Guid() != Guid.Empty)
            {
                // Persist the last position.
                Architect.EditorSettings.CameraPositions[_world.Guid()] = new(
                    _renderContext.Camera.Position.Point(),
                    _renderContext.Camera.Size,
                    EditorHook.CurrentZoomLevel);
            }
        }

        private static void DrawTextRoundedRect(ImDrawListPtr drawList, Vector2 position, Vector4 bgColor, Vector4 textColor, string text)
        {
            drawList.AddRectFilled(position + new Vector2(-4, -2), position + new Vector2(text.Length * 7 + 4, 16),
                ImGuiHelpers.MakeColor32(bgColor), 8f);
            drawList.AddText(position, ImGuiHelpers.MakeColor32(textColor), text);
        }

        private float _targetFixedUpdateTime = 0;

        private void DrawWorld()
        {
            _renderContext.Begin();
            _world.Draw(_renderContext);
            _world.DrawGui(_renderContext);
            _renderContext.End();
        }

        public void Dispose()
        {
            _world.Dispose();
        }

        internal void ResetCamera()
        {
            EditorHook.CurrentZoomLevel = EditorHook.STARTING_ZOOM;
            _renderContext.Camera.Zoom = 1;
            _renderContext.Camera.Position = Vector2.Zero;
        }

        internal void CenterCamera()
        {
            _renderContext.Camera.Position = -_renderContext.Camera.Size / 2f;
        }
        internal void CenterCamera(Vector2 size)
        {
            _renderContext.Camera.Position = -size / 2f;
        }
    }
}