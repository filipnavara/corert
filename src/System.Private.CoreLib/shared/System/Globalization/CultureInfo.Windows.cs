// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if ENABLE_WINRT
using Internal.Runtime.Augments;
#endif
#if FEATURE_APPX
using System.Resources;
using Internal.Resources;
#endif

namespace System.Globalization
{
    public partial class CultureInfo : IFormatProvider
    {
#if FEATURE_APPX
        // When running under AppX, we use this to get some information about the language list
        private static volatile WindowsRuntimeResourceManagerBase s_WindowsRuntimeResourceManager;

        [ThreadStatic]
        private static bool ts_IsDoingAppXCultureInfoLookup;
#endif

        /// <summary>
        /// Gets the default user culture from WinRT, if available.
        /// </summary>
        /// <remarks>
        /// This method may return null, if there is no default user culture or if WinRT isn't available.
        /// </remarks>
        private static CultureInfo GetUserDefaultCultureCacheOverride()
        {
#if ENABLE_WINRT
            WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
            if (callbacks != null && callbacks.IsAppxModel())
            {
                return (CultureInfo)callbacks.GetUserDefaultCulture();
            }
#endif
#if FEATURE_APPX
            if (ApplicationModel.IsUap)
            {
                CultureInfo culture = GetCultureInfoForUserPreferredLanguageInAppX();
                if (culture != null)
                    return culture;
            }
#endif

            return null;
        }

        /// <summary>
        /// Sets the default culture for WinRT.
        /// </summary>
        /// <return>
        /// Returns true if running on WinRT and the culture was successfully set.
        /// </return>
        private static bool SetGlobalDefaultCulture(CultureInfo culture)
        {
#if ENABLE_WINRT
            WinRTInteropCallbacks callbacks = WinRTInterop.UnsafeCallbacks;
            if (callbacks != null && callbacks.IsAppxModel())
            {
                callbacks.SetGlobalDefaultCulture(value);
                return true;
            }
#endif
#if FEATURE_APPX
            if (ApplicationModel.IsUap)
            {
                if (SetCultureInfoForUserPreferredLanguageInAppX(culture))
                {
                    // successfully set the culture, otherwise fallback to legacy path
                    return true;
                }
            }
#endif

            return false;
        }

#if FEATURE_APPX
        internal static CultureInfo GetCultureInfoForUserPreferredLanguageInAppX()
        {
            // If a call to GetCultureInfoForUserPreferredLanguageInAppX() generated a recursive
            // call to itself, return null, since we don't want to stack overflow.  For example,
            // this can happen if some code in this method ends up calling CultureInfo.CurrentCulture.
            // In this case, returning null will mean CultureInfo.CurrentCulture gets the default Win32
            // value, which should be fine.
            if (ts_IsDoingAppXCultureInfoLookup)
            {
                return null;
            }

            CultureInfo toReturn = null;

            try
            {
                ts_IsDoingAppXCultureInfoLookup = true;

                if (s_WindowsRuntimeResourceManager == null)
                {
                    s_WindowsRuntimeResourceManager = ResourceManager.GetWinRTResourceManager();
                }

                toReturn = s_WindowsRuntimeResourceManager.GlobalResourceContextBestFitCultureInfo;
            }
            finally
            {
               ts_IsDoingAppXCultureInfoLookup = false;
            }

            return toReturn;
        }

        internal static bool SetCultureInfoForUserPreferredLanguageInAppX(CultureInfo ci)
        {
            if (s_WindowsRuntimeResourceManager == null)
            {
                s_WindowsRuntimeResourceManager = ResourceManager.GetWinRTResourceManager();
            }

            return s_WindowsRuntimeResourceManager.SetGlobalResourceContextDefaultCulture(ci);
        }
#endif

        internal static CultureInfo GetUserDefaultCulture()
        {
            if (GlobalizationMode.Invariant)
                return CultureInfo.InvariantCulture;

            const uint LOCALE_SNAME = 0x0000005c;
            const string LOCALE_NAME_USER_DEFAULT = null;
            const string LOCALE_NAME_SYSTEM_DEFAULT = "!x-sys-default-locale";

            string strDefault = CultureData.GetLocaleInfoEx(LOCALE_NAME_USER_DEFAULT, LOCALE_SNAME);
            if (strDefault == null)
            {
                strDefault = CultureData.GetLocaleInfoEx(LOCALE_NAME_SYSTEM_DEFAULT, LOCALE_SNAME);

                if (strDefault == null)
                {
                    // If system default doesn't work, use invariant
                    return CultureInfo.InvariantCulture;
                }
            }

            CultureInfo temp = GetCultureByName(strDefault, true);

            temp._isReadOnly = true;

            return temp;
        }

        private static CultureInfo GetUserDefaultUICulture()
        {
#if !ENABLE_WINRT
            if (GlobalizationMode.Invariant)
                return CultureInfo.InvariantCulture;

            const uint MUI_LANGUAGE_NAME = 0x8;    // Use ISO language (culture) name convention
            uint langCount = 0;
            uint bufLen = 0;

            if (Interop.Kernel32.GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out langCount, null, ref bufLen))
            {
                char[] languages = new char[bufLen];
                if (Interop.Kernel32.GetUserPreferredUILanguages(MUI_LANGUAGE_NAME, out langCount, languages, ref bufLen))
                {
                    int index = 0;
                    while (languages[index] != (char)0 && index < languages.Length)
                    {
                        index++;
                    }

                    CultureInfo temp = GetCultureByName(new string(languages, 0, index), true);
                    temp._isReadOnly = true;
                    return temp;
                }
            }
#endif

            return InitializeUserDefaultCulture();
        }
    }
}
