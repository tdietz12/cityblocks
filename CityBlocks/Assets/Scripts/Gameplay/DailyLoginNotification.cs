#if UNITY_ANDROID
using Unity.Notifications.Android;
using Unity.Notifications;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Gameplay
{
    public static class DailyLoginNotification
    {
        private const string ReminderPreference = "daily-login-reminder-enabled";
        private const string NotificationId = "city-blocks-daily-reward";
        private const int AndroidNotificationId = 640127;

        public static bool IsEnabled => PlayerPrefs.GetInt(ReminderPreference, 0) == 1;

        public static void EnableAfterPermission()
        {
            PlayerPrefs.SetInt(ReminderPreference, 1);
            PlayerPrefs.Save();
        #if UNITY_ANDROID
        AndroidNotificationCenter.Initialize();
        NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
        AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
        {
            Id = "daily_rewards",
            Name = "Daily rewards",
            Description = "Daily login reward reminders",
            Importance = Importance.Default
        });
        ScheduleAndroid();
        #elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(NotificationId);
            iOSNotificationCalendarTrigger trigger = new iOSNotificationCalendarTrigger
            {
                Hour = 10,
                Minute = 0,
                Second = 0,
                Repeats = true
            };
            iOSNotificationCenter.ScheduleNotification(new iOSNotification
            {
                Identifier = NotificationId,
                Title = "Your daily reward is ready",
                Body = "Keep your City Blocks login streak going.",
                ShowInForeground = false,
                Trigger = trigger
            });
        #endif
        }

        public static IEnumerator RequestPermissionAndEnable()
        {
        #if UNITY_ANDROID
        AndroidNotificationCenter.Initialize();
        if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
        {
            var permissionRequest = new PermissionRequest();
            while (permissionRequest.Status == PermissionStatus.RequestPending) yield return null;
            if (permissionRequest.Status != PermissionStatus.Allowed) yield break;
        }
        EnableAfterPermission();
        #elif UNITY_IOS
            using (AuthorizationRequest authorization = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false))
            {
                while (!authorization.IsFinished) yield return null;
                if (!authorization.Granted) yield break;
            }
            EnableAfterPermission();
        #else
        Debug.LogWarning("Daily local reminders are supported on Android and iOS builds.");
        yield break;
        #endif
        }

        public static void Disable()
        {
            PlayerPrefs.SetInt(ReminderPreference, 0);
            PlayerPrefs.Save();
        #if UNITY_ANDROID
        AndroidNotificationCenter.CancelNotification(AndroidNotificationId);
        #elif UNITY_IOS
            iOSNotificationCenter.RemoveScheduledNotification(NotificationId);
        #endif
        }

        public static int RewardCreditBalance
        {
            get
            {
                try
                {
                    string path = Path.Combine(Application.persistentDataPath, "daily-login-streak.json");
                    if (!File.Exists(path)) return 0;
                    return JsonUtility.FromJson<DailyLoginStreakData>(File.ReadAllText(path))?.rewardCredits ?? 0;
                }
                catch (Exception exception)
                {
                    Debug.LogError("Could not read daily reward balance: " + exception);
                    return 0;
                }
            }
        }

    #if UNITY_ANDROID
    private static void ScheduleAndroid()
    {
        AndroidNotificationCenter.CancelNotification(AndroidNotificationId);
        DateTime fireTime = DateTime.Now.Date.AddDays(1).AddHours(10);
        var notification = new AndroidNotification
        {
            Title = "Your daily reward is ready",
            Text = "Keep your City Blocks login streak going.",
            FireTime = fireTime,
            RepeatInterval = TimeSpan.FromDays(1)
        };
        AndroidNotificationCenter.SendNotificationWithExplicitID(notification, "daily_rewards", AndroidNotificationId);
    }
    #endif
    }
}
