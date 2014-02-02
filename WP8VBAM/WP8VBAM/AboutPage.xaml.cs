﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;
using PhoneDirect3DXamlAppInterop.Resources;

namespace PhoneDirect3DXamlAppInterop
{
    public partial class AboutPage : PhoneApplicationPage
    {
        public AboutPage()
        {
            InitializeComponent();

            //create ad control
            if (App.HasAds)
            {
                AdControl adControl = new AdControl();
                LayoutRoot.Children.Add(adControl);
                adControl.SetValue(Grid.RowProperty, 2);
            }
            tblkVersion.Text = AppResources.AboutVersion + ": " + System.Reflection.Assembly.GetExecutingAssembly()
                    .FullName.Split('=')[1].Split(',')[0]; 

#if GBC
            contactBlock.Text = AppResources.AboutContact2;
            tblkTitle.Text = AppResources.ApplicationTitle2;
            SystemTray.GetProgressIndicator(this).Text = AppResources.ApplicationTitle2;
#endif
        }

        private void contactBlock_Tap_1(object sender, System.Windows.Input.GestureEventArgs e)
        {
            EmailComposeTask emailcomposer = new EmailComposeTask();
#if !GBC
        	emailcomposer.To = AppResources.AboutContact;
#else
            emailcomposer.To = AppResources.AboutContact2;
#endif
            emailcomposer.Subject = AppResources.EmailSubjectText;
        	emailcomposer.Body = AppResources.EmailBodyText;
        	emailcomposer.Show();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new Uri("/LicensePage.xaml", UriKind.Relative));
        }
    }
}