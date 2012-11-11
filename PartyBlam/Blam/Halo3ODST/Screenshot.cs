﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PartyBlam.IO;

namespace PartyBlam.Blam.Halo3ODST
{
    public class ScreenShot
    {
        private EndianStream _shotStream;
        private Header _shotHeader;
        private Screenshot _shotScreenshot;

        #region Public Access
        public Stream ShotStream
        {
            get { return _shotStream.BaseStream; }
        }
        public Header ShotHeader
        {
            get { return _shotHeader; }
            set { _shotHeader = value; }
        }
        public Screenshot ShotScreenshot
        {
            get { return _shotScreenshot; }
        }
        #endregion

        #region Class Declareation
        public class Header
        {
            public string ScreenshotName { get; set; }
            public string ScreenshotDescription { get; set; }
            public string ScreenshotAuthor { get; set; }
        }

        public class Screenshot
        {
            public Int32 SizeOfEmbeddedScreenshot { get; set; }
            public List<byte> EmbeddedScreenshot { get; set; }
        }
        #endregion

        /// <summary>
        /// Initalize new instance of the Halo 3: ODST Screenshot
        /// </summary>
        /// <param name="shotPath">Path to the Halo 3: ODST 'screen.shot' extracted from a Container file.</param>
        public ScreenShot(string shotPath) { Initalize(new MemoryStream(File.ReadAllBytes(shotPath))); }
        /// <summary>
        /// Initalize new instance of the Halo 3: ODST Screenshot
        /// </summary>
        /// <param name="shotStream">Stream of a Halo 3: ODST 'screen.shot' extracted from a Container file.</param>
        public ScreenShot(Stream shotStream) { Initalize(shotStream); }
        private void Initalize(Stream shotStream)
        {
            _shotStream = new EndianStream(shotStream, Endian.BigEndian);

            if (!isValidScreen())
            {
                Close();
                throw new Exception("Invalid Halo 3: ODST screenshot!");
            }

            // Load Screenshot Parts
            LoadHeader();
            LoadScreenshot();
        }

        #region Loading Code
        /// <summary>
        /// Load the Screenshots Header data
        /// </summary>
        public void LoadHeader()
        {
            _shotHeader = new Header();

            _shotStream.SeekTo(0x48);
            _shotHeader.ScreenshotName = _shotStream.ReadUTF16();

            _shotStream.SeekTo(0x67);
            _shotHeader.ScreenshotDescription = _shotStream.ReadUTF16();

            _shotStream.SeekTo(0xE8);
            _shotHeader.ScreenshotAuthor = _shotStream.ReadAscii();
        }

        /// <summary>
        /// Load the Screenshots Embedded Screenshot data
        /// </summary>
        public void LoadScreenshot()
        {
            _shotScreenshot = new Screenshot();

            _shotStream.SeekTo(0x2B4);
            _shotScreenshot.SizeOfEmbeddedScreenshot = _shotStream.ReadInt32();

            byte[] screenshot = new byte[_shotScreenshot.SizeOfEmbeddedScreenshot];
            _shotStream.ReadBlock(screenshot, 0, _shotScreenshot.SizeOfEmbeddedScreenshot);

            _shotScreenshot.EmbeddedScreenshot = new List<byte>();
            foreach (byte screenshotByte in screenshot)
                _shotScreenshot.EmbeddedScreenshot.Add(screenshotByte);
        }
        #endregion

        #region Update Code
        /// <summary>
        /// Update the Screenshots Header and Embedded Screenshot data
        /// </summary>
        public void Update()
        {
            UpdateHeader();
            UpdateScreenshot();
        }

        /// <summary>
        /// Update the Screenshots Header data
        /// </summary>
        public void UpdateHeader()
        {
            _shotStream.SeekTo(0x48);
            _shotStream.WriteUTF16(_shotHeader.ScreenshotName);

            _shotStream.SeekTo(0x67);
            _shotStream.WriteUTF16(_shotHeader.ScreenshotDescription);

            _shotStream.SeekTo(0xE8);
            _shotStream.WriteAscii(_shotHeader.ScreenshotAuthor);
        }

        /// <summary>
        /// Update the Screenshots Embedded Screenshot data
        /// </summary>
        public void UpdateScreenshot()
        {
            byte[] footerPadding = { 0x5F, 0x65, 0x6F, 0x66, 0x00, 0x00, 0x00, 0x11, 0x00, 
                                       0x01, 0x00, 0x01, 0x00, 0x02, 0xB8, 0x01, 0x00 };

            // Write Length Sets
            _shotStream.SeekTo(0x2B4);
            _shotStream.WriteInt32(_shotScreenshot.SizeOfEmbeddedScreenshot);
            _shotStream.SeekTo(0x10C);
            _shotStream.WriteInt32(_shotScreenshot.SizeOfEmbeddedScreenshot);
            _shotStream.SeekTo(0x144);
            _shotStream.WriteInt32(_shotScreenshot.SizeOfEmbeddedScreenshot);

            // Set new Length
            _shotStream.BaseStream.SetLength(0x2B8 + _shotScreenshot.SizeOfEmbeddedScreenshot + footerPadding.Length);

            // Write Screenshot Data
            _shotStream.SeekTo(0x2B8);
            _shotStream.WriteBlock(_shotScreenshot.EmbeddedScreenshot.ToArray<byte>());

            // Write Footer Data
            _shotStream.SeekTo(0x2B8 + _shotScreenshot.SizeOfEmbeddedScreenshot);
            _shotStream.WriteBlock(footerPadding);
        }
        #endregion

        /// <summary>
        /// Inject a JPEG to a Halo 3: ODST Screenshot
        /// </summary>
        /// <param name="newScreenshot">Bytes of the JPEG</param>
        private void InjectScreenshot(byte[] newScreenshot)
        {
            List<byte> newShot = new List<byte>();
            foreach (byte screenshotByte in newScreenshot)
                newShot.Add(screenshotByte);

            _shotScreenshot.SizeOfEmbeddedScreenshot = newShot.Count;
            _shotScreenshot.EmbeddedScreenshot = newShot;
        }
        /// <summary>
        /// Inject a JPEG to a Halo 3: ODST Screenshot
        /// </summary>
        /// <param name="newScreenshot">Stream of the JPEG</param>
        private void InjectScreenshot(Stream newScreenshot)
        {
            byte[] injectingBytes = ((MemoryStream)newScreenshot).ToArray();

            InjectScreenshot(injectingBytes);
        }

        /// <summary>
        /// Extract a JPEF from a Halo 3: ODST Screenshot
        /// </summary>
        public byte[] ExtractScreenshot()
        {
            byte[] screenshot = new byte[_shotScreenshot.EmbeddedScreenshot.Count];
            screenshot = _shotScreenshot.EmbeddedScreenshot.ToArray<byte>(); ;

            return screenshot;
        }

        public bool isValidScreen()
        {
            _shotStream.SeekTo(0x00);
            string header = _shotStream.ReadAscii(0x04);
            _shotStream.SeekTo(0x0E);
            string blfType = _shotStream.ReadAscii(0x17);

            if (header == "_blf" && blfType == "halo 3 saved screenshot")
                return true;
            else
                return false;
        }
        public void Close()
        {
            _shotStream.Close();
        }
    }
}
