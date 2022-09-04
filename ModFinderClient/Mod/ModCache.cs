﻿using Microsoft.VisualBasic.FileIO;
using ModFinder.UI;
using ModFinder.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModFinder.Mod
{
  public static class ModCache
  {
    private static readonly string CacheDir = Path.Combine(Main.AppFolder, "CachedMods");
    private static readonly string ManifestFile = Path.Combine(CacheDir, "Manifest.json");

    /// <summary>
    /// Directories containing cached mods, indexed by ModId.
    /// </summary>
    private static Dictionary<ModId, string> CachedMods
    {
      get
      {
        _cachedMods ??= LoadManifest();
        return _cachedMods;
      }
    }
    private static Dictionary<ModId, string> _cachedMods;

    private static Dictionary<ModId, string> LoadManifest()
    {
      var cachedMods = new Dictionary<ModId, string>();
      if (!Directory.Exists(CacheDir))
      {
        _ = Directory.CreateDirectory(CacheDir);
      }

      if (File.Exists(ManifestFile))
      {
        IOTool.Safe(
          () =>
          {
            var manifest = IOTool.Read<CacheManifest>(ManifestFile);
            foreach (var mod in manifest.Mods)
            {
              if (Directory.Exists(mod.Dir))
              {
                cachedMods.Add(mod.Id, mod.Dir);
              }
            }
          });
      }

      return cachedMods;
    }

    /// <summary>
    /// Attempts to restore a mod from the local cache.
    /// </summary>
    /// <returns>Install dir if installation succeeded, an empty string otherwise</returns>
    public static bool TryRestoreMod(ModId id)
    {
      if (id.Type != ModType.UMM)
      {
        throw new NotSupportedException($"Currently {id.Type} mods are not supported.");
      }

      if (!CachedMods.ContainsKey(id))
      {
        return false;
      }

      Logger.Log.Info($"Restoring {id.Id} from local cache.");
      var cachePath = CachedMods[id];
      var installPath = Path.Combine(Main.UMMInstallPath, new DirectoryInfo(cachePath).Name);
      FileSystem.CopyDirectory(cachePath, installPath);
      Directory.Delete(cachePath, true);
      CachedMods.Remove(id);
      UpdateManifest();
      return true;
    }

    public static void UninstallAndCache(ModViewModel mod)
    {
      if (mod.Type != ModType.UMM)
      {
        throw new InvalidOperationException($"{mod.Type} is not supported");
      }

      var cachePath = Path.Combine(CacheDir, mod.ModDir.Name);
      Logger.Log.Info($"Uninstalling {mod.Name} and caching at {cachePath}.");
      if (!Directory.Exists(cachePath))
      {
        FileSystem.CopyDirectory(mod.ModDir.FullName, cachePath);
        Directory.Delete(mod.ModDir.FullName, true);
        CachedMods.Add(mod.ModId, cachePath);
        IOTool.Safe(UpdateManifest);
      }
    }

    private static void UpdateManifest()
    {
      var manifest = new CacheManifest(CachedMods.Select(entry => new CachedMod(entry.Key, entry.Value)).ToList());
      IOTool.Write(manifest, ManifestFile);
    }

    private class CacheManifest
    {
      [JsonProperty]
      public readonly List<CachedMod> Mods;

      [JsonConstructor]
      public CacheManifest(List<CachedMod> mods)
      {
        Mods = mods;
      }
    }

    /// <summary>
    /// Identifies a single cached mod.
    /// </summary>
    private class CachedMod
    {
      [JsonProperty]
      public ModId Id { get; }

      [JsonProperty]
      public string Dir { get; }

      public CachedMod(ModId id, string dir)
      {
        Id = id;
        Dir = dir;
      }
    }
  }
}
