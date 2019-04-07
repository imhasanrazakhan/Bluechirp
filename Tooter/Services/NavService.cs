﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace Tooter.Services
{
    public sealed class NavService
    {
        private static Frame _frame = null;
        internal static NavService Instance = new NavService();
        private NavService() { }

        internal static void CreateInstance(Frame frame)
        {
            _frame = frame;
        }

        public void GoBack()
        {
            if (_frame.CanGoBack)
            {
                _frame.GoBack();
            }
        }

        public void GoForward()
        {
            if (_frame.CanGoForward)
            {
                _frame.GoForward();
            }
        }


        public bool Navigate(Type sourcePageType)
        {
            return _frame.Navigate(sourcePageType);
        }

        public bool Navigate(string viewModelName)
        {
            
            bool validNavigation = false;
            string viewName = viewModelName.Replace("ViewModel", "");
            try
            {
                Type viewType = Type.GetType(viewName);
                validNavigation = _frame.Navigate(viewType);
            }
            catch (Exception)
            {

                
            }
           
            return validNavigation;
        }

        public bool Navigate(string viewModelName, object parameter)
        {

            bool validNavigation = false;
            string viewName = viewModelName.Replace("ViewModel", "");
            try
            {
                Type viewType = Type.GetType(viewName);
                validNavigation = _frame.Navigate(viewType, parameter);
            }
            catch (Exception)
            {


            }

            return validNavigation;
        }

        public bool Navigate(string viewModelName, object parameter, NavigationTransitionInfo infoOverride)
        {

            bool validNavigation = false;
            string viewName = viewModelName.Replace("ViewModel", "");
            try
            {
                Type viewType = Type.GetType(viewName);
                validNavigation = _frame.Navigate(viewType, parameter, infoOverride);
            }
            catch (Exception)
            {


            }

            return validNavigation;
        }

        public bool Navigate(Type sourcePageType, object parameter)
        {
            return _frame.Navigate(sourcePageType, parameter);
        }

        public bool Navigate(Type sourcePageType, object parameter, NavigationTransitionInfo infoOverride)
        {
            return _frame.Navigate(sourcePageType, parameter, infoOverride);
        }

        public bool IsCurrentPageOfType(Type typeQuery)
        {
            return _frame.SourcePageType.Equals(typeQuery);
        }
    }
}
