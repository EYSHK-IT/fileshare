﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (C) 2008-2013 ShareX Developers

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

using HelpersLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShareX
{
    public partial class NotificationForm : Form
    {
        public Image ToastImage { get; private set; }
        public string ToastText { get; private set; }
        public string ToastURL { get; private set; }

        private int windowOffset = 3;
        private bool mouseInside = false;
        private bool durationEnd = false;
        private bool closingAnimation = true;
        private int closingAnimationDuration = 2000;
        private int closingAnimationInterval = 50;

        public NotificationForm(int duration, Size size, Image img, string url)
        {
            InitializeComponent();

            img = ImageHelpers.ResizeImageLimit(img, size);
            img = ImageHelpers.DrawCheckers(img);
            ToastImage = img;
            ToastURL = url;

            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            Size = new Size(img.Width + 2, img.Height + 2);
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width - windowOffset, Screen.PrimaryScreen.WorkingArea.Bottom - Height - windowOffset);

            tDuration.Interval = duration;
            tDuration.Start();
        }

        private void tDuration_Tick(object sender, EventArgs e)
        {
            durationEnd = true;
            tDuration.Stop();

            if (!mouseInside)
            {
                StartClosing();
            }
        }

        private void StartClosing()
        {
            if (closingAnimation)
            {
                Opacity = 1;
                tOpacity.Interval = closingAnimationInterval;
                tOpacity.Start();
            }
            else
            {
                Close();
            }
        }

        private void tOpacity_Tick(object sender, EventArgs e)
        {
            float opacityDecrement = (float)closingAnimationInterval / closingAnimationDuration;

            if (Opacity > opacityDecrement)
            {
                Opacity -= opacityDecrement;
            }
            else
            {
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            g.DrawImage(ToastImage, 1, 1, ToastImage.Width, ToastImage.Height);

            if (!string.IsNullOrEmpty(ToastText))
            {
                Rectangle textRect = new Rectangle(0, 0, e.ClipRectangle.Width, 40);

                using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                {
                    g.FillRectangle(brush, textRect);
                }

                using (Font font = new Font("Arial", 10))
                {
                    g.DrawString(ToastText, font, Brushes.Black, textRect.RectangleOffset(-3));
                }
            }

            g.DrawRectangleProper(Pens.Black, e.ClipRectangle);
        }

        public static void Show(int duration, Size size, string imagePath, string url)
        {
            if (duration > 0 && !size.IsEmpty && !string.IsNullOrEmpty(imagePath) && Helpers.IsImageFile(imagePath) && File.Exists(imagePath))
            {
                Image img = ImageHelpers.LoadImage(imagePath);
                NotificationForm form = new NotificationForm(duration, size, img, url);
                NativeMethods.ShowWindow(form.Handle, (int)WindowShowStyle.ShowNoActivate);
                NativeMethods.SetWindowPos(form.Handle, (IntPtr)SpecialWindowHandles.HWND_TOPMOST, 0, 0, 0, 0,
                    SetWindowPosFlags.SWP_NOMOVE | SetWindowPosFlags.SWP_NOSIZE | SetWindowPosFlags.SWP_NOACTIVATE);
            }
        }

        public static void Show(string imagePath, string url)
        {
            Show(4000, new Size(400, 300), imagePath, url);
        }

        private void NotificationForm_MouseClick(object sender, MouseEventArgs e)
        {
            tDuration.Stop();

            if (e.Button == MouseButtons.Left && !string.IsNullOrEmpty(ToastURL))
            {
                Helpers.LoadBrowserAsync(ToastURL);
            }

            Close();
        }

        private void NotificationForm_MouseEnter(object sender, EventArgs e)
        {
            mouseInside = true;

            tOpacity.Stop();
            Opacity = 1;

            ToastText = ToastURL;
            Refresh();
        }

        private void NotificationForm_MouseLeave(object sender, EventArgs e)
        {
            mouseInside = false;

            if (durationEnd)
            {
                StartClosing();
            }
        }

        #region Windows Form Designer generated code

        private System.Windows.Forms.Timer tDuration;
        private System.Windows.Forms.Timer tOpacity;

        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }

            if (ToastImage != null)
            {
                ToastImage.Dispose();
            }

            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tDuration = new System.Windows.Forms.Timer(this.components);
            this.tOpacity = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            //
            // tDuration
            //
            this.tDuration.Tick += new System.EventHandler(this.tDuration_Tick);
            //
            // tOpacity
            //
            this.tOpacity.Tick += new System.EventHandler(this.tOpacity_Tick);
            //
            // NotificationForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.Cursor = System.Windows.Forms.Cursors.Hand;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "NotificationForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "NotificationForm";
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.NotificationForm_MouseClick);
            this.MouseEnter += new System.EventHandler(this.NotificationForm_MouseEnter);
            this.MouseLeave += new System.EventHandler(this.NotificationForm_MouseLeave);
            this.ResumeLayout(false);
        }

        #endregion Windows Form Designer generated code
    }
}