using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using SharpDX;

using Color = System.Drawing.Color;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Events;

namespace LastHit_Assistant
{
    internal static class Program
    {
        private static readonly AIHeroClient = ObjectManager.Player;

        private static int lastAutoAttackTick, lastMovement;

        private static Menu Menu config;

        private static void Main()
        {
            Loading.OnLoadingComplete += delegate
            {
                if (new [] { "Azir", "Kalista" }.Contains(Player.ChampionName))
                {
                    Chat.Print("[ST] LastHit Assistant : " + Player.ChampionName + " is not working properly and has been disabled.");
                    // SadMemes();
                    return;
                }
                config = new Menu("[ST] LastHit Assistant", "ST_LHA", true);

                var menuDrawing = new Menu("Drawings", "LastHit_Draw"); 
                
                config.AddSubMenu(menuDrawing);

                var menuMisc = new Menu("Miscallenous", "LastHit_Misc");
                {
                    menuMisc.AddItem(new MenuItem("LastHit_Misc_Holdzone", "Hold Position").SetValue(new Slider(0, 100, 0)));
                    menuMisc.AddItem(new MenuItem("LastHit_Misc_Farmdelay", "Farm Delay").SetValue(new Slider(0, 100, 0)));
                    menuMisc.AddItem(
                        new MenuItem("LastHit_Misc_ExtraWindUp", "Extra Winduptime").SetValue(new Slider(35, 200, 0)));
                    menuMisc.AddItem(new MenuItem("LastHit_Misc_AutoWindUp", "Autoset Windup").SetValue(false));
                    menuMisc.AddItem(
                        new MenuItem("LastHit_Misc_Humanizer", "Humanizer/Movement Delay").SetValue(new Slider(50, 150, 0)));
                    menuMisc.AddItem(new MenuItem("LastHit_Misc_Path", "Send Short Move Commands").SetValue(true));
                    config.AddItem(new MenuItem("LastHit_Move", "Movement").SetValue(true));
                }
                config.AddSubMenu(menuMisc);

                config.AddItem(
                    new MenuItem("LastHit_Key", "Active").SetValue(new KeyBind('Z', KeyBindType.Press)));

                config.AddToMainMenu();

                Drawing.OnDraw += OnDraw;

                Game.OnUpdate += delegate
                {
                    if (config.Item("LastHit_Key").GetValue<KeyBind>().Active
                        && !Player.IsCastingInterruptableSpell(true) && !Player.IsChannelingImportantSpell()
                        && !MenuGUI.IsChatOpen && !Player.IsDead)
                    {
                        Orbwalk(
                            Game.CursorPos,
                            (from minion in ObjectManager.Get<Obj_AI_Minion>()
                             where Orbwalking.InAutoAttackRange(minion)
                             let t =
                                 (int)(Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2
                                 + 1000 * (int)Player.Distance(minion) / (int)Orbwalking.GetMyProjectileSpeed()
                             let predHealth =
                                 HealthPrediction.GetHealthPrediction(
                                     minion,
                                     t,
                                     config.Item("LastHit_Misc_Farmdelay").GetValue<Slider>().Value)
                             where
                                 minion.Team != GameObjectTeam.Neutral && predHealth > 0
                                 && predHealth <= Player.GetAutoAttackDamage(minion, true)
                             select minion).FirstOrDefault());
                    }

                    if (!config.Item("LastHit_Misc_AutoWindUp").GetValue<bool>())
                    {
                        return;
                    }
                    var additional = 0;
                    if (Game.Ping >= 100)
                    {
                        additional = Game.Ping / 100 * 10;
                    }
                    else if (Game.Ping > 40 && Game.Ping < 100)
                    {
                        additional = Game.Ping / 100 * 20;
                    }
                    else if (Game.Ping <= 40)
                    {
                        additional = +20;
                    }
                    var windUp = Game.Ping + additional;
                    if (windUp < 40)
                    {
                        windUp = 40;
                    }
                    config.Item("LastHit_Misc_ExtraWindUp")
                        .SetValue(windUp < 200 ? new Slider(windUp, 200, 0) : new Slider(200, 200, 0));
                };

                Obj_AI_Base.OnProcessSpellCast += (sender, args) => 
                {
                    if (sender.IsMe && Orbwalking.IsAutoAttack(args.SData.Name))
                    {
                        lastAutoAttackTick = Environment.TickCount;
                    } 
                };
            };
        }

        private static void OnDraw(EventArgs args)
        {
            if (config.Item("LastHit_Draw_AARange").GetValue<Circle>().Active && !Player.IsDead)
            {
                Render.Circle.DrawCircle(
                    Player.Position,
                    Orbwalking.GetRealAutoAttackRange(null),
                    config.Item("LastHit_Draw_AARange").GetValue<Circle>().Color);
            }

            foreach (var minion in MinionManager.GetMinions(Player.Position, 2500).Where(x => x.IsValidTarget()))
            {
                var attackToKill = Math.Ceiling(minion.MaxHealth / Player.GetAutoAttackDamage(minion, true));
                var hpBarPosition = minion.HPBarPosition;
                if (config.Item("LastHit_Draw_Lasthit").GetValue<Circle>().Active
                    && minion.Health <= Player.GetAutoAttackDamage(minion, true))
                {
                    Render.Circle.DrawCircle(
                        minion.Position,
                        minion.BoundingRadius,
                        config.Item("LastHit_Draw_Lasthit").GetValue<Circle>().Color);
                }
                else if (config.Item("LastHit_Draw_nearKill").GetValue<Circle>().Active
                         && minion.Health <= Player.GetAutoAttackDamage(minion, true) * 2)
                {
                    Render.Circle.DrawCircle(
                        minion.Position,
                        minion.BoundingRadius,
                        config.Item("LastHit_Draw_nearKill").GetValue<Circle>().Color);
                }
                if (!config.Item("LastHit_Draw_MinionHPBar").GetValue<Circle>().Active)
                {
                    continue;
                }
                for (var i = 1; i < attackToKill; i++)
                {
                    var startposition = hpBarPosition.X + 45
                                        + (float)
                                          (minion.HasBuff("turretshield") ? 70 : minion.IsMelee ? 75 : 80 / attackToKill)
                                        * i;
                    Drawing.DrawLine(
                        new Vector2(startposition, hpBarPosition.Y + 18),
                        new Vector2(startposition, hpBarPosition.Y + 23),
                        1,
                        config.Item("LastHit_Draw_MinionHPBar").GetValue<Circle>().Color);
                }
            }
        }

        private static void Orbwalk(Vector3 goalPosition, AttackableUnit target)
        {
            if (target != null && CanAttack() && config.Item("LastHit_Key").GetValue<KeyBind>().Active
                && Player.IssueOrder(GameObjectOrder.AttackUnit, target))
            {
                lastAutoAttackTick = Environment.TickCount + Game.Ping / 2;
            }
            else if (config.Item("LastHit_Move").GetValue<bool>() && CanMove())
            {
                MoveTo(goalPosition);
            }
        }

        private static void MoveTo(Vector3 position, float holdAreaRadius = -1)
        {
            var delay = config.Item("LastHit_Misc_Humanizer").GetValue<Slider>().Value;
            if (Environment.TickCount - lastMovement < delay)
            {
                return;
            }
            lastMovement = Environment.TickCount;
            if (holdAreaRadius < 0)
            {
                holdAreaRadius = config.Item("LastHit_Misc_Holdzone").GetValue<Slider>().Value;
            }
            if (Player.ServerPosition.Distance(position) < holdAreaRadius)
            {
                if (Player.Path.Length > 1)
                {
                    Player.IssueOrder(GameObjectOrder.HoldPosition, Player.ServerPosition);
                }
                return;
            }
            if (config.Item("LastHit_Misc_Path").GetValue<bool>())
            {
                Player.IssueOrder(
                    GameObjectOrder.MoveTo,
                    Player.ServerPosition + 200 * (position.To2D() - Player.ServerPosition.To2D()).Normalized().To3D());
            }
            else
            {
                Player.IssueOrder(GameObjectOrder.MoveTo, position);
            }
        }

        private static bool CanAttack() => lastAutoAttackTick <= Environment.TickCount && Environment.TickCount + Game.Ping / 2 + 25 >= lastAutoAttackTick + Player.AttackDelay * 1000;

        private static bool CanMove() => lastAutoAttackTick <= Environment.TickCount && Environment.TickCount + Game.Ping / 2 >= lastAutoAttackTick + Player.AttackCastDelay * 1000 + config.Item("LastHit_Misc_ExtraWindUp").GetValue<Slider>().Value;
    }
}