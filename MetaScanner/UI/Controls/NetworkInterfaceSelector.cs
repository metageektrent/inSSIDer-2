﻿////////////////////////////////////////////////////////////////
//
// Copyright (c) 2007-2010 MetaGeek, LLC
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
//	http://www.apache.org/licenses/LICENSE-2.0 
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License. 
//
////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Drawing;
using System.Timers;
using System.Windows.Forms;
using inSSIDer.Misc;
using inSSIDer.Scanning;
using ManagedWifi;
using inSSIDer.Localization;

namespace inSSIDer.UI.Controls
{
    public partial class NetworkInterfaceSelector : UserControl
    {
        private readonly Bitmap _myStartImage = new Bitmap(Properties.Resources.wifiPlay);
        private readonly Bitmap _myStopImage = new Bitmap(Properties.Resources.wifiStop);

        public event EventHandler<EventArgs> NetworkScanStartEvent;

        public event EventHandler<EventArgs> NetworkScanStopEvent;

        private System.Timers.Timer _myTimer;

        private delegate void UpdateInterfaceListHandler();

        private delegate void DelInterfaceChange(object sender, InterfaceNotificationEventsArgs e);

        private delegate void DelInvokeNoArg();

        private Scanner _scanner;

        public NetworkInterfaceSelector()
        {
            InitializeComponent();
            MaxTextLength = -1;
        }

        public void Initialize(ref Scanner scanner)
        {
            _scanner = scanner;
            if(_scanner == null) return;
            //NetworkController.Instance.Initialize();
            if (Utilities.IsXp())
            {
                _myTimer = new System.Timers.Timer { Interval = 5000.0, Enabled = true };
                _myTimer.Elapsed += MyTimer_Elapsed;
            }
            else if (_scanner.WlanClient != null)
            {
                _scanner.WlanClient.InterfaceArrivedEvent += WlanClient_InterfaceAddedEvent;
                _scanner.WlanClient.InterfaceRemovedEvent += WlanClient_InterfaceRemoveEvent;
            }
            UpdateInterfaceList();
            //if ((this.NetworkInterfaceDropDown.DropDownItems.Count > 0) && Settings.Default.AutoStartWiFi)
            //{
            //    this.StartScan();
            //}
        }

        private void InvokeNetworkScanStartEvent()
        {
            if (NetworkScanStartEvent != null)
            {
                NetworkScanStartEvent(this, EventArgs.Empty);
            }
        }

        private void InvokeNetworkScanStopEvent()
        {
            if (NetworkScanStopEvent != null)
            {
                NetworkScanStopEvent(this, EventArgs.Empty);
            }
        }

        private void WlanClient_InterfaceAddedEvent(object sender, InterfaceNotificationEventsArgs e)
        {
            UpdateInterfaceList();
            //If we are not scanning and a new interface is added, use it!
            if (!_scanner.NetworkScanner.IsScanning && _scanner.SetInterface(e.MyGuid))
            {
                //This will always need to be invoked
                Invoke(new DelInvokeNoArg(StartScan));
            }
        }

        private void WlanClient_InterfaceRemoveEvent(object sender, InterfaceNotificationEventsArgs e)
        {
            if(e.MyGuid == _scanner.WlanInterface.InterfaceGuid)
            {
                //If we were using the interface that got removed, stop scanning!
                if (InvokeRequired)
                    Invoke(new DelInterfaceChange(WlanClient_InterfaceRemoveEvent), new[] {sender, e});
                else
                {
                    StopScan();
                }
            }
            UpdateInterfaceList();
        }

        private void NetworkInterfaceDropDown_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ToolStripMenuItem clickedItem = e.ClickedItem as ToolStripMenuItem;
            if (clickedItem != null)
            {
                NetworkInterfaceDropDown.Text = clickedItem.Text;
                UpdateInterfaceListSelection();
            }
        }

        private void ScanButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (_scanner.NetworkScanner.IsScanning)
                {
                    StopScan();
                }
                else
                {
                    if (ModifierKeys != Keys.Shift)
                    {
                        _scanner.Cache.Clear();
                        Utilities.ResetColor();
                    }
                    StartScan();
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
            }
        }

        private void MyTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            UpdateInterfaceList();
        }

        private void UpdateInterfaceList()
        {
            if (InvokeRequired)
            {
                UpdateInterfaceListHandler method = UpdateInterfaceList;
                Invoke(method);
            }
            else
            {
                lock (this)
                {
                    WlanClient.WlanInterface[] interfaceArray = _scanner.AvalibleWlanInterfaces;
                    if (interfaceArray.Length > 0)
                    {
                        if (!NetworkInterfaceDropDown.Pressed)
                        {
                            NetworkInterfaceDropDown.DropDownItems.Clear();
                            foreach (WlanClient.WlanInterface interface2 in interfaceArray)
                            {
                                NetworkInterfaceDropDown.DropDownItems.Add(interface2.InterfaceDescription);
                            }
                            NetworkInterfaceDropDown.ShowDropDownArrow = NetworkInterfaceDropDown.DropDownItems.Count > 0;
                            UpdateInterfaceListSelection();
                        }
                    }
                    else
                    {
                        NetworkInterfaceDropDown.DropDownItems.Clear();
                        NetworkInterfaceDropDown.Text = Localizer.GetString("NoWiFiInterfacesFound");
                        ScanButton.Enabled = false;
                    }
                }
            }
        }

        private void UpdateInterfaceListSelection()
        {
            bool flag = false;
            foreach (ToolStripMenuItem item in NetworkInterfaceDropDown.DropDownItems)
            {
                if (item.Text.Equals(NetworkInterfaceDropDown.Text))
                {
                    if (_scanner.SetInterface(item.Text))
                    {
                        item.Checked = true;
                        flag = true;
                    }
                }
                else
                {
                    item.Checked = false;
                }
            }
            if (!flag)
            {
                _scanner.StopScanning();
                _scanner.WlanInterface = null;
                if (NetworkInterfaceDropDown.DropDownItems.Count > 0)
                {
                    string text = NetworkInterfaceDropDown.DropDownItems[0].Text;
                    _scanner.SetInterface(text);
                    ((ToolStripMenuItem)NetworkInterfaceDropDown.DropDownItems[0]).Checked = true;
                    NetworkInterfaceDropDown.Text = MaxTextLength > -1 && text.Length > MaxTextLength ? text.Remove(MaxTextLength - 1) + "..." : text;
                }
                else
                {
                    NetworkInterfaceDropDown.Text = Localizer.GetString("NoWirelessInterface");
                }
            }
            NetworkInterfaceDropDown.Enabled = !_scanner.NetworkScanner.IsScanning;
            UpdateScanButtonState(_scanner.NetworkScanner.IsScanning);
        }

        private void UpdateScanButtonState(bool isStarted)
        {
            if (!InvokeRequired)
            {
                if (isStarted)
                {
                    ScanButton.Text = Localizer.GetString("Stop");
                    ScanButton.Image = _myStopImage;
                }
                else
                {
                    ScanButton.Text = Localizer.GetString("Start");
                    ScanButton.Image = _myStartImage;
                }
                ScanButton.Enabled = _scanner.WlanInterface != null;
            }
        }

        /// <summary>
        /// Update control to reflect starting the scan and fire the scan start event
        /// </summary>
        internal void StartScan()
        {
            UpdateScanButtonState(true);
            if (NetworkInterfaceDropDown != null)
            {
                NetworkInterfaceDropDown.Enabled = false;
                InvokeNetworkScanStartEvent();
            }
        }

        /// <summary>
        /// Update control to reflect stopping the scan and fire the scan stop event
        /// </summary>
        internal void StopScan()
        {
            UpdateScanButtonState(false);
            NetworkInterfaceDropDown.Enabled = true;
            InvokeNetworkScanStopEvent();
        }

        /// <summary>
        /// Gets to sets the maximum length of an interface name
        /// </summary>
        [Category("Behavior"),DefaultValue(-1)]
        public int MaxTextLength { get; set; }
        
    }
}
