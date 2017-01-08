﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using ColossalFramework;
using ColossalFramework.DataBinding;
using ColossalFramework.Globalization;
using ColossalFramework.PlatformServices;
using ColossalFramework.Packaging;

namespace FindIt
{
    public class Asset
    {
        public string name;
        public PrefabInfo prefab;
        public ulong steamID;
        public string author;
        public float score;

        public HashSet<string> tagsTitle = new HashSet<string>();
        public HashSet<string> tagsDesc = new HashSet<string>();
        public HashSet<string> tagsHash = new HashSet<string>();

        public static string GetLocalizedTitle(PrefabInfo prefab)
        {
            string result;

            if (prefab is BuildingInfo)
            {
                if (Locale.GetUnchecked("BUILDING_TITLE", prefab.name, out result))
                {
                    return result;
                }
            }
            else if (prefab is PropInfo)
            {
                if (Locale.GetUnchecked("PROPS_TITLE", prefab.name, out result))
                {
                    return result;
                }
            }
            else if (prefab is TreeInfo)
            {
                if (Locale.GetUnchecked("TREE_TITLE", prefab.name, out result))
                {
                    return result;
                }
            }

            string name = prefab.name;

            if (name.Contains("."))
            {
                name = prefab.name.Substring(prefab.name.IndexOf('.') + 1);
            }

            if (name.EndsWith("_Data"))
            {
                name = name.Substring(0, name.LastIndexOf("_Data"));
            }

            return Regex.Replace(name, "([A-Z][a-z]+)", " $1");
        }

        public static string GetLocalizedDescription(PrefabInfo prefab)
        {
            string result;

            if (prefab is BuildingInfo)
            {
                if (Locale.GetUnchecked("BUILDING_DESC", prefab.name, out result))
                {
                    return result;
                }
            }
            else if (prefab is PropInfo)
            {
                if (Locale.GetUnchecked("PROPS_DESC", prefab.name, out result))
                {
                    return result;
                }
            }
            else if (prefab is TreeInfo)
            {
                if (Locale.GetUnchecked("TREE_DESC", prefab.name, out result))
                {
                    return result;
                }
            }

            return "";
        }

        public static string GetLocalizedTooltip(PrefabInfo prefab)
        {
            MilestoneInfo unlockMilestone = prefab.GetUnlockMilestone();

            string text = TooltipHelper.Format(new string[]
	        {
		        LocaleFormatter.Title,
		        Asset.GetLocalizedTitle(prefab),
		        LocaleFormatter.Sprite,
		        (!string.IsNullOrEmpty(prefab.m_InfoTooltipThumbnail)) ? prefab.m_InfoTooltipThumbnail : prefab.name,
		        LocaleFormatter.Text,
		        Asset.GetLocalizedDescription(prefab),
		        LocaleFormatter.Locked,
		        (!ToolsModifierControl.IsUnlocked(unlockMilestone)).ToString()
	        });

            string unlockDesc, currentValue, targetValue, progress, locked;
            ToolsModifierControl.GetUnlockingInfo(unlockMilestone, out unlockDesc, out currentValue, out targetValue, out progress, out locked);

            string addTooltip = TooltipHelper.Format(new string[]
	        {
		        LocaleFormatter.LockedInfo,
		        locked,
		        LocaleFormatter.UnlockDesc,
		        unlockDesc,
		        LocaleFormatter.UnlockPopulationProgressText,
		        progress,
		        LocaleFormatter.UnlockPopulationTarget,
		        targetValue,
		        LocaleFormatter.UnlockPopulationCurrent,
		        currentValue
	        });

            text = TooltipHelper.Append(text, addTooltip);
            PrefabAI aI = prefab.GetAI();
            if (aI != null)
            {
                text = TooltipHelper.Append(text, aI.GetLocalizedTooltip());
            }

            if (prefab is PropInfo || prefab is TreeInfo)
            {
                text = TooltipHelper.Append(text, TooltipHelper.Format(new string[]
	            {
		            LocaleFormatter.Cost,
		            LocaleFormatter.FormatCost(prefab.GetConstructionCost(), false)
	            }));
            }

            return text;
        }
    }

    public class AssetTagList
    {
        public static AssetTagList instance;

        public Dictionary<string, int> tagsTitle = new Dictionary<string, int>();
        public Dictionary<string, int> tagsDesc = new Dictionary<string, int>();
        public Dictionary<string, Asset> assets = new Dictionary<string, Asset>();

        public List<Asset> matches = new List<Asset>();

        public List<Asset> Find(string text)
        {
            matches.Clear();

            text = text.ToLower().Trim();

            if (!text.IsNullOrWhiteSpace())
            {
                string[] tags = Regex.Split(text, @"([^\w]|\s)+", RegexOptions.IgnoreCase);

                foreach (Asset asset in assets.Values)
                {
                    if (asset.prefab != null)
                    {
                        foreach (string t1 in tags)
                        {
                            if (!t1.IsNullOrWhiteSpace())
                            {
                                float score = 0;

                                if (asset.author != null)
                                {
                                    score = 100 * GetScore(t1, asset.author, null);
                                }

                                foreach (string t2 in asset.tagsTitle)
                                {
                                    score += 10 * GetScore(t1, t2, tagsTitle);
                                }

                                foreach (string t2 in asset.tagsDesc)
                                {
                                    score += GetScore(t1, t2, tagsDesc);
                                }

                                if (score > 0)
                                {
                                    asset.score += score;
                                }
                                else
                                {
                                    asset.score = 0;
                                    break;
                                }
                            }
                        }
                    }

                    if (asset.score > 0)
                    {
                        matches.Add(asset);
                    }
                }
                matches = matches.OrderByDescending(s => s.score).ToList();
            }
            else
            {
                foreach (Asset asset in assets.Values)
                {
                    if (asset.prefab != null)
                    {
                        matches.Add(asset);
                    }
                }
                matches = matches.OrderBy(s => s.name).ToList();
            }

            return matches;
        }

        private float GetScore(string t1, string t2, Dictionary<string, int> dico)
        {
            int index = t2.IndexOf(t1);
            float scoreMultiplier = 1f;

            if (index >= 0)
            {
                if (index == 0)
                { 
                    scoreMultiplier = 10f;
                }
                if (dico != null && dico.ContainsKey(t2))
                {
                    return scoreMultiplier / dico[t2] * ((t2.Length - index) / (float)t2.Length) * (t1.Length / (float)t2.Length);
                }
                else
                {
                    if (dico != null) DebugUtils.Log("Tag not found in dico: " + t2);
                    return scoreMultiplier * ((t2.Length - index) / (float)t2.Length) * (t1.Length / (float)t2.Length);
                }
            }

            return 0;
        }

        public AssetTagList()
        {
            foreach (Package.Asset current in PackageManager.FilterAssets(new Package.AssetType[] { UserAssetType.CustomAssetMetaData }))
            {
                PublishedFileId id = current.package.GetPublishedFileID();
                string author = null;

                if (!current.package.packageAuthor.IsNullOrWhiteSpace())
                {
                    ulong authorID;
                    if (UInt64.TryParse(current.package.packageAuthor.Substring("steamid:".Length), out authorID))
                    {
                        author = new Friend(new UserID(authorID)).personaName;
                        author = Regex.Replace(author.ToLower().Trim(), @"([^\w]|\s)+", "_");
                    }
                }

                if (!assets.ContainsKey(current.fullName))
                {
                    assets[current.fullName] = new Asset()
                    {
                        name = current.fullName,
                        steamID = id.AsUInt64,
                        author = author
                    };
                }
            }
        }

        public void Init()
        {
            foreach (Asset asset in assets.Values)
            {
                asset.prefab = null;
            }

            tagsTitle.Clear();
            tagsDesc.Clear();
            
            GetPrefabs<BuildingInfo>();
            //GetPrefab<NetInfo>();
            GetPrefabs<PropInfo>();
            GetPrefabs<TreeInfo>();

            foreach (Asset asset in assets.Values)
            {
                if (asset.prefab != null)
                {
                    asset.tagsTitle = AddAssetTags(asset, tagsTitle, Asset.GetLocalizedTitle(asset.prefab));
                    asset.tagsDesc = AddAssetTags(asset, tagsDesc, Asset.GetLocalizedDescription(asset.prefab));
                }
            }

            CleanDictionary(tagsTitle);
            CleanDictionary(tagsDesc);
        }

        private void GetPrefabs<T>() where T : PrefabInfo
        {
            for (uint i = 0; i < PrefabCollection<T>.PrefabCount(); i++)
            {
                T prefab = PrefabCollection<T>.GetPrefab(i);

                if (prefab == null) continue;

                string name = prefab.name;
                if(name.EndsWith("_Data"))
                {
                    name = name.Substring(0, name.LastIndexOf("_Data"));
                }

                if (assets.ContainsKey(name))
                {
                    assets[name].prefab = prefab;
                }
                else
                {
                    assets[name] = new Asset()
                    {
                        name = name,
                        prefab = prefab,
                        steamID = GetSteamID(prefab)
                    };
                }
            }
        }

        private HashSet<string> AddAssetTags(Asset asset, Dictionary<string, int> dico, string text)
        {
            //text = Regex.Replace(text, "([A-Z][a-z]+)", " $1");

            string[] tagsArr = Regex.Split(text, @"([^\w]|\s)+", RegexOptions.IgnoreCase);

            HashSet<string> tags = new HashSet<string>();

            foreach (string t in tagsArr)
            {
                string tag = t.ToLower().Trim();

                if (tag.Length > 1 && !tag.Contains("_"))
                {
                    if (!dico.ContainsKey(tag))
                    {
                        dico.Add(tag, 0);
                    }
                    dico[tag]++;
                    tags.Add(tag);
                }
            }

            return tags;
        }

        private void CleanDictionary(Dictionary<string, int> dico)
        {
            List<string> keys = new List<string>(dico.Keys);
            foreach (string key in keys)
            {
                if (key.EndsWith("s"))
                {
                    string tag = key.Substring(0, key.Length - 1);
                    if (dico.ContainsKey(tag))
                    {
                        dico[tag] += dico[key];
                        dico.Remove(key);
                    }
                }
            }
        }

        private static ulong GetSteamID(PrefabInfo prefab)
        {
            ulong id = 0;

            if (prefab.name.Contains("."))
            {
                string steamID = prefab.name.Substring(0, prefab.name.IndexOf("."));
                UInt64.TryParse(steamID, out id);
            }

            return id;
        }
    }
}