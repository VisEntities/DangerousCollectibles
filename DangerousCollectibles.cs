/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Facepunch;
using Newtonsoft.Json;
using Rust;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Dangerous Collectibles", "VisEntities", "1.0.1")]
    [Description("Adds a chance for collectibles like stumps to explode when picked up.")]
    public class DangerousCollectibles : RustPlugin
    {
        #region Fields

        private static DangerousCollectibles _plugin;
        private static Configuration _config;

        private const int LAYER_EXPLOSION = Layers.Mask.Player_Server;
        private const string FX_BRADLEY_EXPLOSION = "assets/prefabs/npc/m2bradley/effects/bradley_explosion.prefab";
        private const string FX_MLRS_ROCKET_EXPLOSION_GROUND = "assets/content/vehicles/mlrs/effects/pfx_mlrs_rocket_explosion_ground.prefab";

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Collectibles")]
            public List<CollectibleConfig> Collectibles { get; set; }
        }

        private class CollectibleConfig
        {
            [JsonProperty("Prefab Short Names")]
            public List<string> PrefabShortNames { get; set; }

            [JsonProperty("Remove Collectible Upon Explosion")]
            public bool RemoveCollectibleUponExplosion { get; set; }

            [JsonProperty("Explosion")]
            public ExplosionConfig Explosion { get; set; }
        }

        private class ExplosionConfig
        {
            [JsonProperty("Chance")]
            public int Chance { get; set; }

            [JsonProperty("Impact Radius")]
            public float ImpactRadius { get; set; }

            [JsonProperty("Damage Amount")]
            public float DamageAmount { get; set; }
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                Collectibles = new List<CollectibleConfig>
                {
                    new CollectibleConfig
                    {
                        PrefabShortNames = new List<string>
                        {
                            "stone-collectable",
                            "metal-collectable",
                            "sulfur-collectable",
                            "wood-collectable"
                        },
                        RemoveCollectibleUponExplosion = true,
                        Explosion = new ExplosionConfig
                        {
                            Chance = 10,
                            ImpactRadius = 3,
                            DamageAmount = 20f
                        }
                    },
                    new CollectibleConfig
                    {
                        PrefabShortNames = new List<string>
                        {
                            "corn-collectable",
                            "hemp-collectable",
                            "potato-collectable",
                            "pumpkin-collectable",
                            "mushroom-cluster-5",
                            "mushroom-cluster-6"
                        },
                        RemoveCollectibleUponExplosion = true,
                        Explosion = new ExplosionConfig
                        {
                            Chance = 10,
                            ImpactRadius = 3,
                            DamageAmount = 20f
                        }
                    },
                    new CollectibleConfig
                    {
                        PrefabShortNames = new List<string>
                        {
                            "hemp-collectable"
                        },
                        RemoveCollectibleUponExplosion = true,
                        Explosion = new ExplosionConfig
                        {
                            Chance = 10,
                            ImpactRadius = 3,
                            DamageAmount = 20f
                        }
                    }
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            _config = null;
            _plugin = null;
        }

        private object OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
        {
            if (collectible == null || player == null)
                return null;

            if (PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                return null;

            foreach (CollectibleConfig collectibleConfig in _config.Collectibles)
            {
                if (collectibleConfig.PrefabShortNames.Contains(collectible.ShortPrefabName))
                {
                    if (ChanceSucceeded(collectibleConfig.Explosion.Chance))
                    {
                        Explode(collectible.transform.position, collectibleConfig.Explosion);
                        if (collectibleConfig.RemoveCollectibleUponExplosion)
                        {
                            collectible.Kill();
                            return true;
                        }
                    }
                }
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Explosion

        private void Explode(Vector3 position, ExplosionConfig explosionConfig)
        {
            Effect.server.Run(FX_BRADLEY_EXPLOSION, position, Vector3.up, null, true);
            Effect.server.Run(FX_MLRS_ROCKET_EXPLOSION_GROUND, position, Vector3.up, null, true);

            List<BasePlayer> nearbyPlayers = Pool.Get<List<BasePlayer>>();
            Vis.Entities(position, explosionConfig.ImpactRadius, nearbyPlayers, LAYER_EXPLOSION, QueryTriggerInteraction.Ignore);

            foreach (BasePlayer player in nearbyPlayers)
            {
                if (player == null || PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                    continue;

                HitInfo hitInfo = new HitInfo();
                hitInfo.damageTypes.Add(DamageType.Explosion, explosionConfig.DamageAmount);
                player.Hurt(hitInfo);       
            }

            Pool.FreeUnmanaged(ref nearbyPlayers);
        }

        #endregion Explosion

        #region Helper Functions

        private bool ChanceSucceeded(int chance)
        {
            return Random.Range(0, 100) < chance;
        }

        #endregion Helper Functions

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "dangerouscollectibles.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}