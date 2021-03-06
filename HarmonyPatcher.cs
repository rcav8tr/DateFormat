extern alias ExtendedInfoPanel1;
extern alias ExtendedInfoPanel2;

using UnityEngine;
using CitiesHarmony.API;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections.Generic;
using ColossalFramework.Plugins;

namespace DateFormat
{
    /// <summary>
    /// Harmony patching
    /// </summary>
    internal class HarmonyPatcher
    {
        // Harmony ID unique to this mod
        private const string HarmonyId = "com.github.rcav8tr.DateFormat";

        // whether or not Harmony patches were applied
        private static bool Patched = false;

        /// <summary>
        /// create Harmony patches
        /// </summary>
        public static bool CreatePatches()
        {
            try
            {
                // not patched
                Patched = false;

                // check Harmony
                if (!HarmonyHelper.IsHarmonyInstalled)
                {
                    ColossalFramework.UI.UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel").SetMessage("Missing Dependency",
                        "The Date Format mod requires the 'Harmony (Mod Dependency)' mod.  " + Environment.NewLine + Environment.NewLine +
                        "Please subscribe to the 'Harmony (Mod Dependency)' mod and restart the game.", error: false);
                    return false;
                }

                // patch each routine that has a hard-coded date format
                if (!CreateTranspilerPatch(typeof(UIDateTimeWrapper      ), "Check",                 BindingFlags.Public    | BindingFlags.Instance)) return false;   // main game date
                if (!CreateTranspilerPatch(typeof(ChirpXPanel            ), "UpdateBindings",        BindingFlags.NonPublic | BindingFlags.Instance)) return false;
                if (!CreateTranspilerPatch(typeof(FestivalPanel          ), "RefreshCurrentConcert", BindingFlags.NonPublic | BindingFlags.Instance)) return false;
                if (!CreateTranspilerPatch(typeof(FestivalPanel          ), "RefreshFutureConcert",  BindingFlags.NonPublic | BindingFlags.Instance)) return false;
                if (!CreateTranspilerPatch(typeof(FootballPanel          ), "RefreshMatchInfo",      BindingFlags.NonPublic | BindingFlags.Instance)) return false;
                if (!CreateTranspilerPatch(typeof(VarsitySportsArenaPanel), "RefreshPastMatches",    BindingFlags.NonPublic | BindingFlags.Instance)) return false;
                if (!CreateTranspilerPatch(typeof(VarsitySportsArenaPanel), "RefreshNextMatchDates", BindingFlags.NonPublic | BindingFlags.Instance)) return false;

                // check which Extended InfoPanel mod version is enabled, if any
                bool extendedInfoPanelMod1   = IsModEnabled(781767563L );       // original version, has by far the most subscribers of the 3 versions
                bool extendedInfoPanelMod219 = IsModEnabled(2274354659L);       // 21:9 version of original
                bool extendedInfoPanelMod2   = IsModEnabled(2498761388L);       // updated version of original

                // if Extended InfoPanel mod is enabled, then patch it
                if (extendedInfoPanelMod1 || extendedInfoPanelMod219)
                {
                    CreatePatchExtendedInfoPanel1();
                }
                if (extendedInfoPanelMod2)
                {
                    CreatePatchExtendedInfoPanel2();
                }

                // don't allow an error to prevent the rest of the logic from running
                try
                {
                    // in Bindings, set game time value field to max date time so that next time UIDateTimeWrapper.Check is called
                    // the main game date will be different than the value and the game date will be updated immediately even if the simulation is paused
                    // this is instead of waiting for the game date to change due to the simulation running
                    Bindings bindings = ColossalFramework.UI.UIView.GetAView().GetComponent<Bindings>();
                    FieldInfo fiGameTime = typeof(Bindings).GetField("m_GameTime", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (bindings != null && fiGameTime != null)
                    {
                        UIDateTimeWrapper gameTime = fiGameTime.GetValue(bindings) as UIDateTimeWrapper;
                        FieldInfo fiValue = typeof(UIDateTimeWrapper).GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (gameTime != null && fiValue != null)
                        {
                            fiValue.SetValue(gameTime, DateTime.MaxValue);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                // if Extended InfoPanel mod is enabled, then update game time
                if (extendedInfoPanelMod1 || extendedInfoPanelMod219)
                {
                    UpdateGameTimeExtendedInfoPanel1();
                }
                if (extendedInfoPanelMod2)
                {
                    UpdateGameTimeExtendedInfoPanel2();
                }

                // patch Enhanced Outside Connections View mod
                if (IsModEnabled(2368396560L))
                {
                    CreatePatchEnhancedOutsideConnectionsView();
                }

                // patch More City Statistics mod
                if (IsModEnabled(2685974449L))
                {
                    CreatePatchMoreCityStatistics();
                }

                // the Real Time mod is not patched because the date formatting logic is in class RealTime.UI.DateTooltipBehavior (routine UpdateTooltip)
                // the class is internal, which makes the class inaccessible to be patched by the Harmony Transpiler
                // and I don't want to try to replace or duplicate the logic in UpdateTooltip using Harmony Prefix or Posfix patches

                // success
                Patched = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        // the logic in the following mod patch routines is intentionally separate from the CreatePatches routine because
        // a reference to the patched type in CreatePatches would cause the CreatePatches routine to fail to execute at all when the mod is not present

        /// <summary>
        /// patch Extended InfoPanel mod and Extended InfoPanel 21:9 mod (both mods use the same IINS.ExtendedInfo namespace)
        /// </summary>
        private static void CreatePatchExtendedInfoPanel1()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                CreateTranspilerPatch(typeof(ExtendedInfoPanel1.IINS.ExtendedInfo.CityInfoDatas), "UpdateDate_1", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// patch Extended InfoPanel 2 mod (uses a different namespace than the other 2 mods)
        /// </summary>
        private static void CreatePatchExtendedInfoPanel2()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                CreateTranspilerPatch(typeof(ExtendedInfoPanel2.IINS.ExtendedInfo.CityInfoDatas), "UpdateDate_1", BindingFlags.Public | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// update game time displayed on the Extended InfoPanel mod and Extended InfoPanel 21:9 mod
        /// </summary>
        private static void UpdateGameTimeExtendedInfoPanel1()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                // it is necessary to use FindObjectOfType because using the Singleton<T> syntax causes a run time error when the mod is not present
                ExtendedInfoPanel1.IINS.ExtendedInfo.CityInfoDatas instance =
                    (ExtendedInfoPanel1.IINS.ExtendedInfo.CityInfoDatas)UnityEngine.Object.FindObjectOfType(typeof(ExtendedInfoPanel1.IINS.ExtendedInfo.CityInfoDatas));
                if (instance != null)
                {
                    // call the routine that was patched
                    instance.UpdateDate_1();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// update game time displayed on the Extended InfoPanel 2 mod
        /// </summary>
        private static void UpdateGameTimeExtendedInfoPanel2()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                // it is necessary to use FindObjectOfType because using the Singleton<T> syntax causes a run time error when the mod is not present
                ExtendedInfoPanel2.IINS.ExtendedInfo.CityInfoDatas instance =
                    (ExtendedInfoPanel2.IINS.ExtendedInfo.CityInfoDatas)UnityEngine.Object.FindObjectOfType(typeof(ExtendedInfoPanel2.IINS.ExtendedInfo.CityInfoDatas));
                if (instance != null)
                {
                    // call the routine that was patched
                    instance.UpdateDate_1();
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// patch Enhanced Outside Connection View mod
        /// </summary>
        private static void CreatePatchEnhancedOutsideConnectionsView()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                CreateTranspilerPatch(typeof(EnhancedOutsideConnectionsView.EOCVGraph), "OnTooltipHover", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// patch More City Statistics mod
        /// </summary>
        private static void CreatePatchMoreCityStatistics()
        {
            // don't allow an error here to prevent the rest of this mod from working
            try
            {
                // update the two places where dates are used
                CreateTranspilerPatch(typeof(MoreCityStatistics.UIImprovedGraph), "OnTooltipHover",    BindingFlags.NonPublic | BindingFlags.Instance);
                CreateTranspilerPatch(typeof(MoreCityStatistics.ShowRange      ), "UpdateSliderLabel", BindingFlags.NonPublic | BindingFlags.Instance);

                // call routine to update slider labels:  MoreCityStatistics.ShowRange.instance.UpdateSliderLabels
                PropertyInfo property = typeof(MoreCityStatistics.ShowRange).GetProperty("instance", BindingFlags.Static | BindingFlags.Public);
                if (property == null)
                {
                    Debug.LogError("Unable to find MoreCityStatistics.ShowRange.instance property.");
                }
                else
                {
                    MoreCityStatistics.ShowRange instance = (MoreCityStatistics.ShowRange)property.GetValue(null, null);
                    if (instance == null)
                    {
                        Debug.LogError("Unable to get value for MoreCityStatistics.ShowRange.instance.");
                    }
                    else
                    {
                        MethodInfo method = typeof(MoreCityStatistics.ShowRange).GetMethod("UpdateSliderLabels", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (method == null)
                        {
                            Debug.LogError("Unable to get MoreCityStatistics.ShowRange.UpdateSliderLabels method.");
                        }
                        else
                        {
                            method.Invoke(instance, null);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// create a transpiler patch
        /// </summary>
        /// <param name="originalType">type that contains the method to be patched</param>
        /// <param name="originalMethodName">name of the method to be patched</param>
        /// <param name="bindingFlags">binding flags of the method to be patched</param>
        /// <returns>success status</returns>
        private static bool CreateTranspilerPatch(Type originalType, string originalMethodName, BindingFlags bindingFlags)
        {
            // get the original method
            MethodInfo originalMethod = originalType.GetMethod(originalMethodName, bindingFlags);
            if (originalMethod == null)
            {
                Debug.LogError($"Unable to find original method {originalType.Name}.{originalMethodName}.");
                return false;
            }

            // get the transpiler method
            MethodInfo transpilerMethod = typeof(HarmonyPatcher).GetMethod("ReplaceDateFormatString", BindingFlags.Static | BindingFlags.NonPublic);
            if (transpilerMethod == null)
            {
                Debug.LogError($"Unable to find patch transpiler method HarmonyPatcher.ReplaceDateFormatString.");
                return false;
            }

            // create the patch
            new Harmony(HarmonyId).Patch(originalMethod, null, null, new HarmonyMethod(transpilerMethod));

            // success
            return true;
        }

        /// <summary>
        /// find and replace hard-coded date formats with the configured date format
        /// </summary>
        private static IEnumerable<CodeInstruction> ReplaceDateFormatString(IEnumerable<CodeInstruction> instructions)
        {
            // get the configured date format
            DateFormatConfiguration config = Configuration<DateFormatConfiguration>.Load();
            string dateFormat = config.BuildDateFormatString();

            // copy instructions to new code
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);

            // find and replace all occurrences of "dd/MM/yyyy" and "yyyy-MM-dd" with the configured date format
            for (int i = 0; i < code.Count - 1; i++)
            {
                if (code[i].opcode == System.Reflection.Emit.OpCodes.Ldstr)
                {
                    string operand = (string)code[i].operand;
                    if (operand == "dd/MM/yyyy" || operand == "yyyy-MM-dd")
                    {
                        code[i].operand = dateFormat;
                    }
                }
            }

            // return the updated code
            return code;
        }

        /// <summary>
        /// remove Harmony patches
        /// </summary>
        public static void RemovePatches()
        {
            try
            {
                if (HarmonyHelper.IsHarmonyInstalled)
                {
                    new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                    Patched = false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// remove and recreate patches
        /// </summary>
        public static void ReapplyPatches()
        {
            if (Patched)
            {
                // remove and recreate patches
                RemovePatches();
                CreatePatches();
            }
        }

        /// <summary>
        /// return whether or not the specified mod is enabled
        /// </summary>
        public static bool IsModEnabled(ulong modID)
        {
            // determine if Date Reformatter mod is enabled
            foreach (PluginManager.PluginInfo mod in PluginManager.instance.GetPluginsInfo())
            {
                // ignore builtin mods and camera script
                if (!mod.isBuiltin && !mod.isCameraScript)
                {
                    // check against the Date REformatter workshop ID
                    if (mod.publishedFileID.AsUInt64 == modID)
                    {
                        // found the mod, return enabled status
                        return mod.isEnabled;
                    }
                }
            }

            // not found, so not enabled
            return false;
        }
    }
}
