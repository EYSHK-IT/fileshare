﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2016 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using ShareX.ScreenCaptureLib.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    public class ShapeManager
    {
        public List<BaseShape> Shapes { get; private set; } = new List<BaseShape>();

        private BaseShape currentShape;

        public BaseShape CurrentShape
        {
            get
            {
                return currentShape;
            }
            private set
            {
                currentShape = value;
                OnCurrentShapeChanged(currentShape);
            }
        }

        private ShapeType currentShapeType;

        public ShapeType CurrentShapeType
        {
            get
            {
                return currentShapeType;
            }
            private set
            {
                currentShapeType = value;
                config.CurrentShapeType = currentShapeType;
                DeselectArea();
                OnCurrentShapeTypeChanged(currentShapeType);
            }
        }

        public Rectangle CurrentRectangle
        {
            get
            {
                if (CurrentShape != null)
                {
                    return CurrentShape.Rectangle;
                }

                return Rectangle.Empty;
            }
        }

        public BaseShape[] Regions
        {
            get
            {
                return Shapes.OfType<BaseRegionShape>().ToArray();
            }
        }

        public BaseDrawingShape[] DrawingShapes
        {
            get
            {
                return Shapes.OfType<BaseDrawingShape>().ToArray();
            }
        }

        public BaseEffectShape[] EffectShapes
        {
            get
            {
                return Shapes.OfType<BaseEffectShape>().ToArray();
            }
        }

        public BaseShape[] ValidRegions
        {
            get
            {
                return Regions.Where(x => IsAreaValid(x.Rectangle)).ToArray();
            }
        }

        public bool IsCurrentRegionValid
        {
            get
            {
                return IsAreaValid(CurrentRectangle);
            }
        }

        public Rectangle CurrentHoverRectangle { get; private set; }

        public bool IsCurrentHoverAreaValid
        {
            get
            {
                return !CurrentHoverRectangle.IsEmpty;
            }
        }

        public bool IsCurrentShapeTypeRegion
        {
            get
            {
                return CurrentShapeType == ShapeType.RegionRectangle || CurrentShapeType == ShapeType.RegionRoundedRectangle || CurrentShapeType == ShapeType.RegionEllipse;
            }
        }

        public Point CurrentPosition { get; private set; }
        public Point PositionOnClick { get; private set; }

        public ResizeManager ResizeManager { get; private set; }
        public bool IsCreating { get; private set; }
        public bool IsMoving { get; private set; }

        public bool IsResizing
        {
            get
            {
                return ResizeManager.IsResizing;
            }
        }

        public bool IsProportionalResizing { get; private set; }
        public bool IsSnapResizing { get; private set; }

        public List<SimpleWindowInfo> Windows { get; set; }
        public bool WindowCaptureMode { get; set; }
        public bool IncludeControls { get; set; }
        public int MinimumSize { get; set; } = 3;

        public event Action<BaseShape> CurrentShapeChanged;
        public event Action<ShapeType> CurrentShapeTypeChanged;

        private RectangleRegionForm form;
        private SurfaceOptions config;
        private ContextMenuStrip cmsContextMenu;

        public ShapeManager(RectangleRegionForm form)
        {
            this.form = form;
            config = form.Config;

            ResizeManager = new ResizeManager(form, this);

            form.MouseDown += form_MouseDown;
            form.MouseUp += form_MouseUp;
            form.KeyDown += form_KeyDown;
            form.KeyUp += form_KeyUp;
            form.MouseWheel += form_MouseWheel;

            if (form.Mode == RectangleRegionMode.Annotation)
            {
                CreateContextMenu();
            }

            CurrentShape = null;
            CurrentShapeType = ShapeType.RegionRectangle; //config.CurrentShapeType;
        }

        private void CreateContextMenu()
        {
            cmsContextMenu = new ContextMenuStrip(form.components);

            ToolStripMenuItem tsmiCancelCapture = new ToolStripMenuItem("Cancel capture");
            tsmiCancelCapture.Image = Resources.prohibition;
            tsmiCancelCapture.Click += (sender, e) => form.Close(RegionResult.Close);
            cmsContextMenu.Items.Add(tsmiCancelCapture);

            ToolStripMenuItem tsmiCloseMenu = new ToolStripMenuItem("Close menu");
            tsmiCloseMenu.Image = Resources.cross;
            tsmiCloseMenu.Click += (sender, e) => cmsContextMenu.Close();
            cmsContextMenu.Items.Add(tsmiCloseMenu);

            ToolStripSeparator tssObjectOptions = new ToolStripSeparator();
            cmsContextMenu.Items.Add(tssObjectOptions);

            ToolStripMenuItem tsmiDeleteSelected = new ToolStripMenuItem("Delete selected object");
            tsmiDeleteSelected.Image = Resources.layer__minus;
            tsmiDeleteSelected.Click += (sender, e) => RemoveCurrentArea();
            cmsContextMenu.Items.Add(tsmiDeleteSelected);

            ToolStripMenuItem tsmiDeleteAll = new ToolStripMenuItem("Delete all objects");
            tsmiDeleteAll.Image = Resources.minus;
            tsmiDeleteAll.Click += (sender, e) => ClearAll();
            cmsContextMenu.Items.Add(tsmiDeleteAll);

            cmsContextMenu.Items.Add(new ToolStripSeparator());

            foreach (ShapeType shapeType in Helpers.GetEnums<ShapeType>())
            {
                ToolStripMenuItem tsmiShapeType = new ToolStripMenuItem(shapeType.GetLocalizedDescription());

                Image img = null;

                switch (shapeType)
                {
                    case ShapeType.RegionRectangle:
                        img = Resources.layer_shape_region;
                        break;
                    case ShapeType.RegionRoundedRectangle:
                        img = Resources.layer_shape_round_region;
                        break;
                    case ShapeType.RegionEllipse:
                        img = Resources.layer_shape_ellipse_region;
                        break;
                    case ShapeType.DrawingRectangle:
                        img = Resources.layer_shape;
                        break;
                    case ShapeType.DrawingRoundedRectangle:
                        img = Resources.layer_shape_round;
                        break;
                    case ShapeType.DrawingEllipse:
                        img = Resources.layer_shape_ellipse;
                        break;
                    case ShapeType.DrawingLine:
                        img = Resources.layer_shape_line;
                        break;
                    case ShapeType.DrawingArrow:
                        img = Resources.layer_shape_arrow;
                        break;
                    case ShapeType.DrawingBlur:
                        img = Resources.layer_shade;
                        break;
                    case ShapeType.DrawingPixelate:
                        img = Resources.grid;
                        break;
                    case ShapeType.DrawingHighlight:
                        img = Resources.highlighter_text;
                        break;
                }

                tsmiShapeType.Image = img;

                tsmiShapeType.Checked = shapeType == CurrentShapeType;
                tsmiShapeType.Tag = shapeType;
                tsmiShapeType.Click += (sender, e) =>
                {
                    tsmiShapeType.RadioCheck();
                    CurrentShapeType = shapeType;
                };
                cmsContextMenu.Items.Add(tsmiShapeType);
            }

            ToolStripSeparator tssShapeOptions = new ToolStripSeparator();
            cmsContextMenu.Items.Add(tssShapeOptions);

            ToolStripMenuItem tsmiBorderColor = new ToolStripMenuItem("Border color...");
            tsmiBorderColor.Click += (sender, e) =>
            {
                form.Pause();

                using (ColorPickerForm dialogColor = new ColorPickerForm(config.ShapeBorderColor))
                {
                    if (dialogColor.ShowDialog() == DialogResult.OK)
                    {
                        config.ShapeBorderColor = dialogColor.NewColor;
                        if (tsmiBorderColor.Image != null) tsmiBorderColor.Image.Dispose();
                        tsmiBorderColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeBorderColor, new Rectangle(0, 0, 16, 16));
                        UpdateCurrentShape();
                    }
                }

                form.Resume();
            };
            tsmiBorderColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeBorderColor, new Rectangle(0, 0, 16, 16));
            cmsContextMenu.Items.Add(tsmiBorderColor);

            ToolStripLabeledNumericUpDown tslnudBorderSize = new ToolStripLabeledNumericUpDown();
            tslnudBorderSize.LabeledNumericUpDownControl.Text = "Border size:";
            tslnudBorderSize.LabeledNumericUpDownControl.Minimum = 1;
            tslnudBorderSize.LabeledNumericUpDownControl.Maximum = 20;
            tslnudBorderSize.LabeledNumericUpDownControl.Value = config.ShapeBorderSize;
            tslnudBorderSize.LabeledNumericUpDownControl.ValueChanged = (sender, e) =>
            {
                config.ShapeBorderSize = (int)tslnudBorderSize.LabeledNumericUpDownControl.Value;
                UpdateCurrentShape();
            };
            cmsContextMenu.Items.Add(tslnudBorderSize);

            ToolStripMenuItem tsmiFillColor = new ToolStripMenuItem("Fill color...");
            tsmiFillColor.Click += (sender, e) =>
            {
                form.Pause();

                using (ColorPickerForm dialogColor = new ColorPickerForm(config.ShapeFillColor))
                {
                    if (dialogColor.ShowDialog() == DialogResult.OK)
                    {
                        config.ShapeFillColor = dialogColor.NewColor;
                        if (tsmiFillColor.Image != null) tsmiFillColor.Image.Dispose();
                        tsmiFillColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeFillColor, new Rectangle(0, 0, 16, 16));
                        UpdateCurrentShape();
                    }
                }

                form.Resume();
            };
            tsmiFillColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeFillColor, new Rectangle(0, 0, 16, 16));
            cmsContextMenu.Items.Add(tsmiFillColor);

            ToolStripLabeledNumericUpDown tslnudRoundedRectangleRadius = new ToolStripLabeledNumericUpDown();
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Text = "Corner radius:";
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Minimum = 0;
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Maximum = 150;
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Increment = 3;
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Value = config.ShapeRoundedRectangleRadius;
            tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.ValueChanged = (sender, e) =>
            {
                config.ShapeRoundedRectangleRadius = (int)tslnudRoundedRectangleRadius.LabeledNumericUpDownControl.Value;
                UpdateCurrentShape();
            };
            cmsContextMenu.Items.Add(tslnudRoundedRectangleRadius);

            ToolStripLabeledNumericUpDown tslnudBlurRadius = new ToolStripLabeledNumericUpDown();
            tslnudBlurRadius.LabeledNumericUpDownControl.Text = "Blur radius:";
            tslnudBlurRadius.LabeledNumericUpDownControl.Minimum = 2;
            tslnudBlurRadius.LabeledNumericUpDownControl.Maximum = 100;
            tslnudBlurRadius.LabeledNumericUpDownControl.Value = config.ShapeBlurRadius;
            tslnudBlurRadius.LabeledNumericUpDownControl.ValueChanged = (sender, e) =>
            {
                config.ShapeBlurRadius = (int)tslnudBlurRadius.LabeledNumericUpDownControl.Value;
                UpdateCurrentShape();
            };
            cmsContextMenu.Items.Add(tslnudBlurRadius);

            ToolStripLabeledNumericUpDown tslnudPixelateSize = new ToolStripLabeledNumericUpDown();
            tslnudPixelateSize.LabeledNumericUpDownControl.Text = "Pixel size:";
            tslnudPixelateSize.LabeledNumericUpDownControl.Minimum = 2;
            tslnudPixelateSize.LabeledNumericUpDownControl.Maximum = 100;
            tslnudPixelateSize.LabeledNumericUpDownControl.Value = config.ShapeRoundedRectangleRadius;
            tslnudPixelateSize.LabeledNumericUpDownControl.ValueChanged = (sender, e) =>
            {
                config.ShapePixelateSize = (int)tslnudPixelateSize.LabeledNumericUpDownControl.Value;
                UpdateCurrentShape();
            };
            cmsContextMenu.Items.Add(tslnudPixelateSize);

            ToolStripMenuItem tsmiHighlightColor = new ToolStripMenuItem("Highlight color...");
            tsmiHighlightColor.Click += (sender, e) =>
            {
                form.Pause();

                using (ColorPickerForm dialogColor = new ColorPickerForm(config.ShapeHighlightColor))
                {
                    if (dialogColor.ShowDialog() == DialogResult.OK)
                    {
                        config.ShapeHighlightColor = dialogColor.NewColor;
                        if (tsmiHighlightColor.Image != null) tsmiHighlightColor.Image.Dispose();
                        tsmiHighlightColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeHighlightColor, new Rectangle(0, 0, 16, 16));
                        UpdateCurrentShape();
                    }
                }

                form.Resume();
            };
            tsmiHighlightColor.Image = ImageHelpers.CreateColorPickerIcon(config.ShapeHighlightColor, new Rectangle(0, 0, 16, 16));
            cmsContextMenu.Items.Add(tsmiHighlightColor);

            cmsContextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem tsmiFullscreenCapture = new ToolStripMenuItem("Capture fullscreen");
            tsmiFullscreenCapture.Image = Resources.layer_fullscreen;
            tsmiFullscreenCapture.Click += (sender, e) => form.Close(RegionResult.Fullscreen);
            cmsContextMenu.Items.Add(tsmiFullscreenCapture);

            ToolStripMenuItem tsmiActiveMonitorCapture = new ToolStripMenuItem("Capture active monitor");
            tsmiActiveMonitorCapture.Image = Resources.monitor;
            tsmiActiveMonitorCapture.Click += (sender, e) => form.Close(RegionResult.ActiveMonitor);
            cmsContextMenu.Items.Add(tsmiActiveMonitorCapture);

            ToolStripMenuItem tsmiMonitorCapture = new ToolStripMenuItem("Capture monitor");
            tsmiMonitorCapture.HideImageMargin();
            tsmiMonitorCapture.Image = Resources.monitor_window;
            cmsContextMenu.Items.Add(tsmiMonitorCapture);

            tsmiMonitorCapture.DropDownItems.Clear();

            Screen[] screens = Screen.AllScreens;

            for (int i = 0; i < screens.Length; i++)
            {
                Screen screen = screens[i];
                ToolStripMenuItem tsmi = new ToolStripMenuItem(string.Format("{0}. {1}x{2}", i + 1, screen.Bounds.Width, screen.Bounds.Height));
                int index = i;
                tsmi.Click += (sender, e) =>
                {
                    form.MonitorIndex = index;
                    form.Close(RegionResult.Monitor);
                };
                tsmiMonitorCapture.DropDownItems.Add(tsmi);
            }

            cmsContextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem tsmiOptions = new ToolStripMenuItem("Options");
            tsmiOptions.Image = Resources.gear;
            cmsContextMenu.Items.Add(tsmiOptions);

            ToolStripMenuItem tsmiQuickCrop = new ToolStripMenuItem("Multi region mode");
            tsmiQuickCrop.Checked = !config.QuickCrop;
            tsmiQuickCrop.CheckOnClick = true;
            tsmiQuickCrop.Click += (sender, e) => config.QuickCrop = !tsmiQuickCrop.Checked;
            tsmiOptions.DropDownItems.Add(tsmiQuickCrop);

            ToolStripMenuItem tsmiShowInfo = new ToolStripMenuItem("Show position and size info");
            tsmiShowInfo.Checked = config.ShowInfo;
            tsmiShowInfo.CheckOnClick = true;
            tsmiShowInfo.Click += (sender, e) => config.ShowInfo = tsmiShowInfo.Checked;
            tsmiOptions.DropDownItems.Add(tsmiShowInfo);

            ToolStripMenuItem tsmiShowMagnifier = new ToolStripMenuItem("Show magnifier");
            tsmiShowMagnifier.Checked = config.ShowMagnifier;
            tsmiShowMagnifier.CheckOnClick = true;
            tsmiShowMagnifier.Click += (sender, e) => config.ShowMagnifier = tsmiShowMagnifier.Checked;
            tsmiOptions.DropDownItems.Add(tsmiShowMagnifier);

            ToolStripMenuItem tsmiUseSquareMagnifier = new ToolStripMenuItem("Square shape magnifier");
            tsmiUseSquareMagnifier.Checked = config.UseSquareMagnifier;
            tsmiUseSquareMagnifier.CheckOnClick = true;
            tsmiUseSquareMagnifier.Click += (sender, e) => config.UseSquareMagnifier = tsmiUseSquareMagnifier.Checked;
            tsmiOptions.DropDownItems.Add(tsmiUseSquareMagnifier);

            ToolStripLabeledNumericUpDown tslnudMagnifierPixelCount = new ToolStripLabeledNumericUpDown();
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Text = "Magnifier pixel count:";
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Minimum = 1;
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Maximum = 35;
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Increment = 2;
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Value = config.MagnifierPixelCount;
            tslnudMagnifierPixelCount.LabeledNumericUpDownControl.ValueChanged = (sender, e) => config.MagnifierPixelCount = (int)tslnudMagnifierPixelCount.LabeledNumericUpDownControl.Value;
            tsmiOptions.DropDownItems.Add(tslnudMagnifierPixelCount);

            ToolStripLabeledNumericUpDown tslnudMagnifierPixelSize = new ToolStripLabeledNumericUpDown();
            tslnudMagnifierPixelSize.LabeledNumericUpDownControl.Text = "Magnifier pixel size:";
            tslnudMagnifierPixelSize.LabeledNumericUpDownControl.Minimum = 2;
            tslnudMagnifierPixelSize.LabeledNumericUpDownControl.Maximum = 30;
            tslnudMagnifierPixelSize.LabeledNumericUpDownControl.Value = config.MagnifierPixelSize;
            tslnudMagnifierPixelSize.LabeledNumericUpDownControl.ValueChanged = (sender, e) => config.MagnifierPixelSize = (int)tslnudMagnifierPixelSize.LabeledNumericUpDownControl.Value;
            tsmiOptions.DropDownItems.Add(tslnudMagnifierPixelSize);

            ToolStripMenuItem tsmiShowCrosshair = new ToolStripMenuItem("Show screen wide crosshair");
            tsmiShowCrosshair.Checked = config.ShowCrosshair;
            tsmiShowCrosshair.CheckOnClick = true;
            tsmiShowCrosshair.Click += (sender, e) => config.ShowCrosshair = tsmiShowCrosshair.Checked;
            tsmiOptions.DropDownItems.Add(tsmiShowCrosshair);

            ToolStripMenuItem tsmiShowFPS = new ToolStripMenuItem("Show FPS");
            tsmiShowFPS.Checked = config.ShowFPS;
            tsmiShowFPS.CheckOnClick = true;
            tsmiShowFPS.Click += (sender, e) => config.ShowFPS = tsmiShowFPS.Checked;
            tsmiOptions.DropDownItems.Add(tsmiShowFPS);

            CurrentShapeChanged += shape =>
            {
                tssObjectOptions.Visible = tsmiDeleteAll.Visible = Shapes.Count > 0;
                tsmiDeleteSelected.Visible = shape != null;
            };

            CurrentShapeTypeChanged += shapeType =>
            {
                foreach (ToolStripMenuItem tsmi in cmsContextMenu.Items.OfType<ToolStripMenuItem>().Where(x => x.Tag is ShapeType))
                {
                    if ((ShapeType)tsmi.Tag == shapeType)
                    {
                        tsmi.RadioCheck();
                        break;
                    }
                }

                switch (shapeType)
                {
                    default:
                        tssShapeOptions.Visible = false;
                        break;
                    case ShapeType.RegionRoundedRectangle:
                    case ShapeType.DrawingRectangle:
                    case ShapeType.DrawingRoundedRectangle:
                    case ShapeType.DrawingEllipse:
                    case ShapeType.DrawingLine:
                    case ShapeType.DrawingArrow:
                    case ShapeType.DrawingBlur:
                    case ShapeType.DrawingPixelate:
                    case ShapeType.DrawingHighlight:
                        tssShapeOptions.Visible = true;
                        break;
                }

                switch (shapeType)
                {
                    default:
                        tsmiBorderColor.Visible = false;
                        tslnudBorderSize.Visible = false;
                        break;
                    case ShapeType.DrawingRectangle:
                    case ShapeType.DrawingRoundedRectangle:
                    case ShapeType.DrawingEllipse:
                    case ShapeType.DrawingLine:
                    case ShapeType.DrawingArrow:
                        tsmiBorderColor.Visible = true;
                        tslnudBorderSize.Visible = true;
                        break;
                }

                switch (shapeType)
                {
                    default:
                        tsmiFillColor.Visible = false;
                        break;
                    case ShapeType.DrawingRectangle:
                    case ShapeType.DrawingRoundedRectangle:
                    case ShapeType.DrawingEllipse:
                        tsmiFillColor.Visible = true;
                        break;
                }

                tslnudRoundedRectangleRadius.Visible = shapeType == ShapeType.RegionRoundedRectangle || shapeType == ShapeType.DrawingRoundedRectangle;
                tslnudBlurRadius.Visible = shapeType == ShapeType.DrawingBlur;
                tslnudPixelateSize.Visible = shapeType == ShapeType.DrawingPixelate;
                tsmiHighlightColor.Visible = shapeType == ShapeType.DrawingHighlight;
            };
        }

        private void form_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!IsCreating)
                {
                    RegionSelection(e.Location);
                }
            }
        }

        private void form_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (IsMoving || IsCreating)
                {
                    EndRegionSelection();
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                if (IsCreating)
                {
                    CancelRegionSelection();
                    EndRegionSelection();
                }
                else if (form.Mode == RectangleRegionMode.Annotation && cmsContextMenu != null)
                {
                    cmsContextMenu.Show(form, e.Location.Add(-10, -10));
                    config.ShowMenuTip = false;
                }
                else
                {
                    form.Close(RegionResult.Close);
                }
            }
        }

        private void form_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Insert:
                    if (IsCreating)
                    {
                        EndRegionSelection();
                    }
                    else
                    {
                        if (ResizeManager.Visible)
                        {
                            DeselectArea();
                        }

                        if (CurrentShape == null || CurrentShape != AreaIntersect())
                        {
                            RegionSelection(InputManager.MousePosition);
                        }
                    }
                    break;
                case Keys.ShiftKey:
                    IsProportionalResizing = true;
                    break;
                case Keys.Menu:
                    IsSnapResizing = true;
                    break;
                case Keys.NumPad0:
                    CurrentShapeType = ShapeType.RegionRectangle;
                    break;
                case Keys.NumPad1:
                    CurrentShapeType = ShapeType.DrawingRectangle;
                    break;
                case Keys.NumPad2:
                    CurrentShapeType = ShapeType.DrawingRoundedRectangle;
                    break;
                case Keys.NumPad3:
                    CurrentShapeType = ShapeType.DrawingEllipse;
                    break;
                case Keys.NumPad4:
                    CurrentShapeType = ShapeType.DrawingLine;
                    break;
                case Keys.NumPad5:
                    CurrentShapeType = ShapeType.DrawingArrow;
                    break;
                case Keys.NumPad6:
                    CurrentShapeType = ShapeType.DrawingBlur;
                    break;
                case Keys.NumPad7:
                    CurrentShapeType = ShapeType.DrawingPixelate;
                    break;
                case Keys.NumPad8:
                    CurrentShapeType = ShapeType.DrawingHighlight;
                    break;
            }
        }

        private void form_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.ShiftKey:
                    IsProportionalResizing = false;
                    break;
                case Keys.Menu:
                    IsSnapResizing = false;
                    break;
                case Keys.Delete:
                    RemoveCurrentArea();

                    if (IsCreating)
                    {
                        EndRegionSelection();
                    }
                    break;
            }
        }

        private void form_MouseWheel(object sender, MouseEventArgs e)
        {
            if (e.Delta > 0)
            {
                CurrentShapeType = CurrentShapeType.Previous<ShapeType>();
            }
            else if (e.Delta < 0)
            {
                CurrentShapeType = CurrentShapeType.Next<ShapeType>();
            }
        }

        public void Update()
        {
            BaseShape shape = CurrentShape;

            if (shape != null)
            {
                if (IsMoving)
                {
                    ResizeManager.MoveCurrentArea(InputManager.MouseVelocity.X, InputManager.MouseVelocity.Y);
                }
                else if (IsCreating && !CurrentRectangle.IsEmpty)
                {
                    CurrentPosition = InputManager.MousePosition0Based;

                    Point newPosition = CurrentPosition;

                    if (IsProportionalResizing)
                    {
                        newPosition = CaptureHelpers.ProportionalPosition(PositionOnClick, CurrentPosition);
                    }

                    if (IsSnapResizing)
                    {
                        newPosition = SnapPosition(PositionOnClick, newPosition);
                    }

                    shape.EndPosition = newPosition;
                }
            }

            CheckHover();

            ResizeManager.Update();
        }

        private Point SnapPosition(Point posOnClick, Point posCurrent)
        {
            Rectangle currentRect = CaptureHelpers.CreateRectangle(posOnClick, posCurrent);
            Point newPosition = posCurrent;

            foreach (SnapSize size in config.SnapSizes)
            {
                if (currentRect.Width.IsBetween(size.Width - config.SnapDistance, size.Width + config.SnapDistance) ||
                    currentRect.Height.IsBetween(size.Height - config.SnapDistance, size.Height + config.SnapDistance))
                {
                    newPosition = CaptureHelpers.CalculateNewPosition(posOnClick, posCurrent, size);
                    break;
                }
            }

            Rectangle newRect = CaptureHelpers.CreateRectangle(posOnClick, newPosition);

            if (form.ScreenRectangle0Based.Contains(newRect))
            {
                return newPosition;
            }

            return posCurrent;
        }

        private void CheckHover()
        {
            CurrentHoverRectangle = Rectangle.Empty;

            if (!ResizeManager.IsCursorOnNode() && !IsCreating && !IsMoving && !IsResizing)
            {
                Rectangle hoverArea = GetAreaIntersectWithMouse();

                if (!hoverArea.IsEmpty)
                {
                    CurrentHoverRectangle = hoverArea;
                }
                else
                {
                    SimpleWindowInfo window = FindSelectedWindow();

                    if (window != null && !window.Rectangle.IsEmpty)
                    {
                        hoverArea = CaptureHelpers.ScreenToClient(window.Rectangle);
                        CurrentHoverRectangle = Rectangle.Intersect(form.ScreenRectangle0Based, hoverArea);
                    }
                }
            }
        }

        public SimpleWindowInfo FindSelectedWindow()
        {
            if (Windows != null)
            {
                return Windows.FirstOrDefault(x => x.Rectangle.Contains(InputManager.MousePosition));
            }

            return null;
        }

        public WindowInfo FindSelectedWindowInfo(Point mousePosition)
        {
            if (Windows != null)
            {
                SimpleWindowInfo windowInfo = Windows.FirstOrDefault(x => x.IsWindow && x.Rectangle.Contains(mousePosition));

                if (windowInfo != null)
                {
                    return windowInfo.WindowInfo;
                }
            }

            return null;
        }

        private void RegionSelection(Point location)
        {
            if (ResizeManager.IsCursorOnNode())
            {
                return;
            }

            BaseShape shape = AreaIntersect(InputManager.MousePosition0Based);

            PositionOnClick = InputManager.MousePosition0Based;

            if (shape != null && shape.ShapeType == CurrentShapeType) // Select area
            {
                IsMoving = true;
                CurrentShape = shape;
                SelectArea();
            }
            else if (!IsCreating) // Create new area
            {
                DeselectArea();

                Rectangle rect;

                if (config.IsFixedSize)
                {
                    IsMoving = true;
                    rect = new Rectangle(new Point(location.X - config.FixedSize.Width / 2, location.Y - config.FixedSize.Height / 2), config.FixedSize);
                }
                else
                {
                    IsCreating = true;
                    rect = new Rectangle(location, new Size(1, 1));
                }

                AddRegionShape(rect);

                CurrentShape.StartPosition = PositionOnClick;
            }
        }

        private void EndRegionSelection()
        {
            IsCreating = false;
            IsMoving = false;

            if (!CurrentRectangle.IsEmpty)
            {
                if (!IsCurrentRegionValid)
                {
                    RemoveCurrentArea();
                    CheckHover();
                }
                else if (config.QuickCrop && IsCurrentShapeTypeRegion)
                {
                    form.UpdateRegionPath();
                    form.Close(RegionResult.Region);
                }
                else
                {
                    SelectArea();
                }
            }

            if (!CurrentHoverRectangle.IsEmpty)
            {
                AddRegionShape(CurrentHoverRectangle);

                if (config.QuickCrop && IsCurrentShapeTypeRegion)
                {
                    form.UpdateRegionPath();
                    form.Close(RegionResult.Region);
                }
                else
                {
                    SelectArea();
                }
            }
        }

        private void AddRegionShape(Rectangle rect)
        {
            BaseShape shape = CreateRegionShape(rect);
            Shapes.Add(shape);
            CurrentShape = shape;
        }

        public BaseShape CreateRegionShape(Rectangle rect)
        {
            BaseShape shape;

            switch (CurrentShapeType)
            {
                default:
                case ShapeType.RegionRectangle:
                    shape = new RectangleRegionShape();
                    break;
                case ShapeType.RegionRoundedRectangle:
                    shape = new RoundedRectangleRegionShape();
                    break;
                case ShapeType.RegionEllipse:
                    shape = new EllipseRegionShape();
                    break;
                case ShapeType.DrawingRectangle:
                    shape = new RectangleDrawingShape();
                    break;
                case ShapeType.DrawingRoundedRectangle:
                    shape = new RoundedRectangleDrawingShape();
                    break;
                case ShapeType.DrawingEllipse:
                    shape = new EllipseDrawingShape();
                    break;
                case ShapeType.DrawingLine:
                    shape = new LineDrawingShape();
                    break;
                case ShapeType.DrawingArrow:
                    shape = new ArrowDrawingShape();
                    break;
                case ShapeType.DrawingBlur:
                    shape = new BlurEffectShape();
                    break;
                case ShapeType.DrawingPixelate:
                    shape = new PixelateEffectShape();
                    break;
                case ShapeType.DrawingHighlight:
                    shape = new HighlightEffectShape();
                    break;
            }

            shape.Rectangle = rect;

            UpdateShape(shape);

            return shape;
        }

        private void UpdateCurrentShape()
        {
            UpdateShape(CurrentShape);
        }

        private void UpdateShape(BaseShape shape)
        {
            if (shape != null)
            {
                if (shape is BaseDrawingShape)
                {
                    BaseDrawingShape baseDrawingShape = (BaseDrawingShape)shape;
                    baseDrawingShape.BorderColor = config.ShapeBorderColor;
                    baseDrawingShape.BorderSize = config.ShapeBorderSize;
                    baseDrawingShape.FillColor = config.ShapeFillColor;
                }

                if (shape is IRoundedRectangleShape)
                {
                    IRoundedRectangleShape roundedRectangleShape = (IRoundedRectangleShape)shape;
                    roundedRectangleShape.Radius = config.ShapeRoundedRectangleRadius;
                }
                else if (shape is BlurEffectShape)
                {
                    BlurEffectShape blurDrawingShape = (BlurEffectShape)shape;
                    blurDrawingShape.BlurRadius = config.ShapeBlurRadius;
                }
                else if (shape is PixelateEffectShape)
                {
                    PixelateEffectShape pixelateDrawingShape = (PixelateEffectShape)shape;
                    pixelateDrawingShape.PixelSize = config.ShapePixelateSize;
                }
                else if (shape is HighlightEffectShape)
                {
                    HighlightEffectShape highlightDrawingShape = (HighlightEffectShape)shape;
                    highlightDrawingShape.HighlightColor = config.ShapeHighlightColor;
                }
            }
        }

        public Image RenderOutputImage(Image img)
        {
            Bitmap bmp = new Bitmap(img);

            if (DrawingShapes.Length > 0 || EffectShapes.Length > 0)
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    foreach (BaseEffectShape shape in EffectShapes)
                    {
                        if (shape != null)
                        {
                            shape.DrawFinal(g, bmp);
                        }
                    }

                    foreach (BaseDrawingShape shape in DrawingShapes)
                    {
                        if (shape != null)
                        {
                            shape.Draw(g);
                        }
                    }
                }
            }

            return bmp;
        }

        private void SelectArea()
        {
            if (!CurrentRectangle.IsEmpty && !config.IsFixedSize)
            {
                ResizeManager.Show();
            }
        }

        private void DeselectArea()
        {
            CurrentShape = null;
            ResizeManager.Hide();
        }

        private void CancelRegionSelection()
        {
            BaseShape shape = AreaIntersect();

            if (shape != null)
            {
                Shapes.Remove(shape);
                DeselectArea();
            }
        }

        private void RemoveCurrentArea()
        {
            BaseShape shape = CurrentShape;

            if (shape != null)
            {
                Shapes.Remove(shape);
                DeselectArea();
            }
        }

        private void ClearAll()
        {
            Shapes.Clear();
            DeselectArea();
        }

        private bool IsAreaValid(Rectangle rect)
        {
            return !rect.IsEmpty && rect.Width >= MinimumSize && rect.Height >= MinimumSize;
        }

        public BaseShape AreaIntersect(Point mousePosition)
        {
            for (int i = Shapes.Count - 1; i >= 0; i--)
            {
                BaseShape shape = Shapes[i];

                if (shape.ShapeType == CurrentShapeType && shape.Rectangle.Contains(mousePosition))
                {
                    return shape;
                }
            }

            return null;
        }

        public BaseShape AreaIntersect()
        {
            return AreaIntersect(InputManager.MousePosition0Based);
        }

        public Rectangle GetAreaIntersectWithMouse()
        {
            BaseShape shape = AreaIntersect();

            if (shape != null)
            {
                return shape.Rectangle;
            }

            return Rectangle.Empty;
        }

        public bool IsAreaIntersect()
        {
            return AreaIntersect() != null;
        }

        public Rectangle CombineAreas()
        {
            BaseShape[] areas = ValidRegions;

            if (areas.Length > 0)
            {
                Rectangle rect = areas[0].Rectangle;

                for (int i = 1; i < areas.Length; i++)
                {
                    rect = Rectangle.Union(rect, areas[i].Rectangle);
                }

                return rect;
            }

            return Rectangle.Empty;
        }

        private void OnCurrentShapeChanged(BaseShape shape)
        {
            if (CurrentShapeChanged != null)
            {
                CurrentShapeChanged(shape);
            }
        }

        private void OnCurrentShapeTypeChanged(ShapeType shapeType)
        {
            if (CurrentShapeTypeChanged != null)
            {
                CurrentShapeTypeChanged(shapeType);
            }
        }
    }
}