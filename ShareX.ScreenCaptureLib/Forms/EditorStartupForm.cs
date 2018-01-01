﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2018 ShareX Team

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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    public partial class EditorStartupForm : Form
    {
        public RegionCaptureOptions Options { get; private set; }
        public Image Image { get; private set; }
        public string ImageFilePath { get; private set; }

        public EditorStartupForm(RegionCaptureOptions options)
        {
            InitializeComponent();
            Icon = ShareXResources.Icon;
            Options = options;
        }

        private void btnOpenImageFile_Click(object sender, EventArgs e)
        {
            string ImageFilePath = ImageHelpers.OpenImageFileDialog(this);

            if (!string.IsNullOrEmpty(ImageFilePath) && File.Exists(ImageFilePath))
            {
                Image = ImageHelpers.LoadImage(ImageFilePath);

                if (Image != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }

            ImageFilePath = null;
        }

        private void btnLoadImageFromClipboard_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsImage())
            {
                Image = ClipboardHelpers.GetImage();

                if (Image != null)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            else
            {
                MessageBox.Show("Clipboard does not contains an image.", "ShareX", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnCreateNewImage_Click(object sender, EventArgs e)
        {
            Image = NewImageForm.CreateNewImage(Options, this);

            if (Image != null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}