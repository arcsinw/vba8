﻿using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Windows.Storage;

namespace PhoneDirect3DXamlAppInterop
{
    public class IsoImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                BitmapImage bitmap = new BitmapImage();
                string path = (string)value;
                if (path.Equals(FileHandler.DEFAULT_SNAPSHOT) || path.Equals(FileHandler.DEFAULT_SNAPSHOT_ALT))
                    return path;
            
                if (!String.IsNullOrEmpty(path))
                {
                    using (var isoStore = IsolatedStorageFile.GetUserStoreForApplication())
                    {
                        using (IsolatedStorageFileStream fs = isoStore.OpenFile(path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                        {
                            bitmap.SetSource(fs);
                        }
                    }
                }
                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }

            
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
