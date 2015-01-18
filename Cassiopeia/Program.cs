using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

namespace Cassiopeia
{
    class Program
    {
        static readonly Obj_AI_Hero player = ObjectManager.Player;
        static Orbwalking.Orbwalker Orbwalker;
        static Spell Q;
        static Spell W;
        static Spell E;
        static Spell R;
        static SpellSlot IgniteSlot = player.GetSpellSlot("SummonerDot");
        static int dontUseQW = -1;
        static float dontUseQW2 = 0;
        static float castWafter = 0;
        static float castWafter2 = 0;
        static Obj_AI_Hero selectedTarget = null;

        static Menu menu = new Menu("Cassiopeia", "Cassiopeia", true);

        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += OnGameLoad;
        }

        static void OnGameLoad(EventArgs args)
        {
            if (player.ChampionName != "Cassiopeia") return;

            Game.PrintChat("Cassiopeia combo by Huntera");

            Q = new Spell(SpellSlot.Q, 850f);
            Q.SetSkillshot(0.6f, 40f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            W = new Spell(SpellSlot.W, 850f);
            W.SetSkillshot(0.5f, 90f, 2500, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 700f);
            E.SetTargetted(0.2f, float.MaxValue);
            
            R = new Spell(SpellSlot.R, 800f);
            R.SetSkillshot(0.6f, (float)(80 * Math.PI / 180), float.MaxValue, false, SkillshotType.SkillshotCone);

            menu.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));
            Orbwalker = new Orbwalking.Orbwalker(menu.SubMenu("Orbwalking"));

            menu.AddSubMenu(new Menu("Harass", "Harass"));
            menu.SubMenu("Harass").AddItem(new MenuItem("qHarass", "Use Q").SetValue(true));
            menu.SubMenu("Harass").AddItem(new MenuItem("wHarass", "Use W").SetValue(false));
            menu.SubMenu("Harass").AddItem(new MenuItem("eHarass", "Use E").SetValue(true));

            menu.AddSubMenu(new Menu("LineClear", "LineClear"));
            menu.SubMenu("LineClear").AddItem(new MenuItem("lineclearQ", "Use Q").SetValue(true));
            menu.SubMenu("LineClear").AddItem(new MenuItem("lineclearW", "Use W").SetValue(false));
            menu.SubMenu("LineClear").AddItem(new MenuItem("onlylasthitE", "Only last hit E").SetValue(true));

            menu.AddSubMenu(new Menu("JungleClear", "JungleClear"));
            menu.SubMenu("JungleClear").AddItem(new MenuItem("jungleclearQ", "Use Q").SetValue(true));
            menu.SubMenu("JungleClear").AddItem(new MenuItem("jungleclearW", "Use W").SetValue(false));

            menu.AddSubMenu(new Menu("Misc", "Misc"));
            menu.SubMenu("Misc").AddItem(new MenuItem("focusSelectedTarget", "Focus Selected Target").SetValue(true));
            menu.SubMenu("Misc").AddItem(new MenuItem("useAAcombo", "Use AA in Combo").SetValue(true));
            menu.SubMenu("Misc").AddItem(new MenuItem("PacketCast", "Use Packets").SetValue(true));

            menu.AddToMainMenu();
            
            Game.OnGameUpdate += OnGameUpdate;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            //Game.OnGameSendPacket += Game_OnGameSendPacket;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += Interrupter_OnPossibleToInterrupt;
            Game.OnWndProc += GameOnOnWndProc;
            Drawing.OnDraw += DrawingOnOnDraw;
        }

        static void OnGameUpdate(EventArgs args)
        {
            try
            {
                switch (Orbwalker.ActiveMode)
                {
                    case Orbwalking.OrbwalkingMode.Combo:
                        Combo();
                        break;
                    case Orbwalking.OrbwalkingMode.Mixed:
                        EFarm();
                        Combo(true);
                        break;
                    case Orbwalking.OrbwalkingMode.LaneClear:
                        LineClear();
                        JungleClear();
                        break;
                    case Orbwalking.OrbwalkingMode.LastHit:
                        EFarm();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private static void DrawingOnOnDraw(EventArgs args)
        {
            if (!menu.Item("focusSelectedTarget").GetValue<bool>()) return;

            if (selectedTarget.IsValidTarget())
            {
                Render.Circle.DrawCircle(selectedTarget.Position, 150, Color.Red, 7, true);
            }
        }

        private static void GameOnOnWndProc(WndEventArgs args)
        {
            if (!menu.Item("focusSelectedTarget").GetValue<bool>()) return;

            if (args.Msg != (uint)WindowsMessages.WM_LBUTTONDOWN)
                return;

            selectedTarget = ObjectManager.Get<Obj_AI_Hero>()
                    .Where(hero => hero.IsValidTarget() && hero.Distance(Game.CursorPos, true) < 40000)
                    .OrderBy(h => h.Distance(Game.CursorPos, true)).FirstOrDefault();
        }

        static void Game_OnGameSendPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] == Packet.C2S.Cast.Header)
            {
                var decodedPacket = Packet.C2S.Cast.Decoded(args.PacketData);
                if (decodedPacket.SourceNetworkId == player.NetworkId && decodedPacket.Slot == SpellSlot.R)
                {
                    Vector3 vecCast = new Vector3(decodedPacket.ToX, decodedPacket.ToY, 0);
                    var query = GetEnemyList().Where(x => R.WillHit(x, vecCast));

                    if (query.Count() == 0)
                    {
                        args.Process = false;
                    }
                }
            }
        }

        static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (gapcloser.Sender.IsValidTarget(R.Range) && R.IsReady())
                R.Cast(gapcloser.Sender.ServerPosition, menu.Item("PacketCast").GetValue<bool>());
        }

        static void Interrupter_OnPossibleToInterrupt(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (unit.IsValidTarget(R.Range) && spell.DangerLevel >= InterruptableDangerLevel.High)
                R.CastIfHitchanceEquals(unit, unit.IsMoving ? HitChance.High : HitChance.Medium, menu.Item("PacketCast").GetValue<bool>());
        }

        static void Orbwalking_BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                Boolean useAAcombo = menu.Item("useAAcombo").GetValue<bool>();

                args.Process = useAAcombo;

                if (useAAcombo)
                {
                    if (args.Target is Obj_AI_Base)
                    {
                        var target = args.Target as Obj_AI_Base;
                        if (E.IsReady() && target.HasBuffOfType(BuffType.Poison) && target.IsValidTarget(E.Range))
                            args.Process = false;
                    }
                }
            }
        }

        private static void EFarm()
        {
            List<Obj_AI_Base> MinionList = MinionManager.GetMinions(player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.Health);
            foreach (var minion in MinionList.Where(x => x.HasBuffOfType(BuffType.Poison)))
            {
                var buffEndTime = GetPoisonBuffEndTime(minion);
                if (buffEndTime > Game.Time + E.Delay)
                {
                    if (getEDmg(minion) >= minion.Health * 1.1)
                        E.CastOnUnit(minion, menu.Item("PacketCast").GetValue<bool>());
                }
            }
        }

        private static void LineClear()
        {
            List<Obj_AI_Base> mobs = MinionManager.GetMinions(player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth).ToList();

            if (!mobs.Any())
                return;

            Boolean packetCast = menu.Item("PacketCast").GetValue<bool>();

            if (E.IsReady())
            {
                Obj_AI_Base mob = mobs.Where(x => x.HasBuffOfType(BuffType.Poison) && x.IsValidTarget(E.Range) && GetPoisonBuffEndTime(x) > (Game.Time + E.Delay) && (!menu.Item("onlylasthitE").GetValue<bool>() || getEDmg(x) >= x.Health * 1.1)).FirstOrDefault();
                if (mob != null)
                    E.CastOnUnit(mob, packetCast);
            }

            if (Q.IsReady() && menu.Item("lineclearQ").GetValue<bool>())
            {
                MinionManager.FarmLocation Qunpoisoned = Q.GetCircularFarmLocation(mobs.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList(), Q.Width * 0.95f);
                if (Qunpoisoned.MinionsHit > 0)
                {
                    Q.Cast(Qunpoisoned.Position, packetCast);
                    castWafter2 = Game.Time + Q.Delay;
                    return;
                }
            }

            if (W.IsReady() && menu.Item("lineclearW").GetValue<bool>() && castWafter2 < Game.Time)
            {
                MinionManager.FarmLocation Qunpoisoned = Q.GetCircularFarmLocation(mobs.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList(), W.Width);
                if (Qunpoisoned.MinionsHit > 0)
                    W.Cast(Qunpoisoned.Position, packetCast);
            }
        }

        private static void JungleClear()
        {
            List<Obj_AI_Base> mobs = MinionManager.GetMinions(player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth).ToList();

            if (!mobs.Any())
                return;

            Boolean packetCast = menu.Item("PacketCast").GetValue<bool>();
            
            MinionManager.FarmLocation mf = Q.GetCircularFarmLocation(mobs, Q.Width * 0.90f);

            if (Q.IsReady() && menu.Item("jungleclearQ").GetValue<bool>())
            {
                if (mf.MinionsHit > 1)
                    Q.Cast(mf.Position, packetCast);
                else
                    Q.Cast(mobs.FirstOrDefault(), packetCast);

                castWafter2 = Game.Time + Q.Delay;
            }

            if (W.IsReady() && menu.Item("jungleclearW").GetValue<bool>() && castWafter2 < Game.Time)
            {
                MinionManager.FarmLocation Qunpoisoned = Q.GetCircularFarmLocation(mobs.Where(x => !x.HasBuffOfType(BuffType.Poison)).ToList(), W.Width);
                if (Qunpoisoned.MinionsHit > 0)
                    W.Cast(Qunpoisoned.Position, packetCast);
            }

            if (E.IsReady())
            {
                Obj_AI_Base mob = mobs.Where(x => x.HasBuffOfType(BuffType.Poison) && x.IsValidTarget(E.Range) && GetPoisonBuffEndTime(x) > (Game.Time + E.Delay)).FirstOrDefault();
                if (mob != null)
                    E.CastOnUnit(mob, packetCast);
            }
            
        }

        private static void Combo(Boolean harass = false)
        {
            Boolean checkTarget = true;

            if (menu.Item("focusSelectedTarget").GetValue<bool>())
            {
                if (selectedTarget.IsValidTarget())
                {
                    checkTarget = false;

                    if (dontUseQW2 > Game.ClockTime)
                        if (selectedTarget.NetworkId == dontUseQW)
                            checkTarget = true;
                }
            }

            if (menu.Item("useAAcombo").GetValue<bool>())
            {
                if (checkTarget)
                    Orbwalker.ForceTarget(GetEnemyList().Where(x => x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(x))).OrderBy(x => x.Health / getEDmg(x)).FirstOrDefault());
                else
                    Orbwalker.ForceTarget(selectedTarget);
            }

            Boolean packetCast = menu.Item("PacketCast").GetValue<bool>();

            if (R.IsReady() && !harass)
            {
                Obj_AI_Hero enemyR = GetEnemyList().Where(x => player.Distance(x.Position) <= 650f && !x.IsInvulnerable).OrderBy(x => x.Health / getRDmg(x)).FirstOrDefault();
                if (enemyR != null)
                {
                    PredictionOutput castPred = R.GetPrediction(enemyR, true, R.Range);

                    List<Obj_AI_Hero> enemiesHit = GetEnemyList().Where(x => R.WillHit(x, castPred.CastPosition)).ToList();
                    int countList = enemiesHit.Count();
                    int facingEnemies = 0;
                    foreach (Obj_AI_Hero hero in enemiesHit)
                        if (hero.IsFacing(player))
                            facingEnemies++;


                    if ((countList >= 2 && facingEnemies >= 1) || countList >= 3 || (countList == 1 && player.Level < 14))
                    {
                        Boolean ult = true;

                        if (countList == 1)
                        {
                            ult = false;
                            int multipleE = (facingEnemies == 1) ? 4 : 2;
                            int multipleQ = (facingEnemies == 1) ? 2 : 1;
                            double procHealth = (getEDmg(enemyR) * multipleE + getQDmg(enemyR) * multipleQ + getRDmg(enemyR)) / enemyR.Health;
                            if (procHealth > 1 && procHealth < 1.5)
                                ult = true;
                        }

                        if (ult) R.Cast(castPred.CastPosition, packetCast);
                    }
                }
            }

            if (E.IsReady() && (!harass || menu.Item("eHarass").GetValue<bool>()))
            {
                Obj_AI_Hero mainTarget = null;

                if (checkTarget)
                {
                    List<Obj_AI_Hero> Eenemies = GetEnemyList();

                    Obj_AI_Hero eTarget = null;
                    Obj_AI_Hero eTarget2 = null;

                    if (Eenemies.Count() > 0)
                    {
                        double minCast1 = -1;
                        double minCast2 = -1;

                        foreach (Obj_AI_Hero enemyto in Eenemies)
                        {
                            if (!enemyto.IsValidTarget(E.Range)) continue;

                            Boolean buffedEnemy = false;
                            if (enemyto.HasBuffOfType(BuffType.Poison))
                            {
                                var buffEndTime = GetPoisonBuffEndTime(enemyto);
                                if (buffEndTime > Game.Time + E.Delay)
                                    buffedEnemy = true;
                            }

                            if (buffedEnemy)
                            {
                                double casts = enemyto.Health / getEDmg(enemyto);
                                if (minCast1 == -1 || minCast1 > casts)
                                {
                                    minCast1 = casts;
                                    eTarget = enemyto;
                                }
                            }
                            else if (getEDmg(enemyto) > enemyto.Health * 1.03)
                            {
                                float dist = player.Distance(enemyto.Position);
                                if (minCast2 == -1 || minCast2 < dist)
                                {
                                    minCast2 = dist;
                                    eTarget2 = enemyto;
                                }
                            }
                        }
                    }

                    mainTarget = (eTarget != null) ? eTarget : eTarget2;
                }
                else
                {
                    if (player.Distance(selectedTarget.Position) <= E.Range && ((selectedTarget.HasBuffOfType(BuffType.Poison) && GetPoisonBuffEndTime(selectedTarget) > Game.Time + E.Delay) || getEDmg(selectedTarget) > selectedTarget.Health * 1.03))
                        mainTarget = selectedTarget;
                }

                if (mainTarget != null)
                {
                    Orbwalker.ForceTarget(mainTarget);

                    if (E.Cast(mainTarget, packetCast) == Spell.CastStates.SuccessfullyCasted)
                    {
                        if (getEDmg(mainTarget) > mainTarget.Health * 1.1)
                        {
                            dontUseQW = mainTarget.NetworkId;
                            dontUseQW2 = Game.ClockTime + 0.6f;
                        }
                    }
                }
            }

            Obj_AI_Hero enemy = (!checkTarget) ? selectedTarget : getTarget(Q.Range);
            
            if (enemy != null)
            {
                if (IgniteSlot != SpellSlot.Unknown && player.Spellbook.CanUseSpell(IgniteSlot) == SpellState.Ready)
                {
                    if (player.Distance(enemy.Position) <= 600f)
                        if (ObjectManager.Player.GetSummonerSpellDamage(enemy, Damage.SummonerSpell.Ignite) / enemy.Health > 1.05)
                            player.Spellbook.CastSpell(IgniteSlot, enemy);
                }

                if (Q.IsReady() && (!harass || menu.Item("qHarass").GetValue<bool>()))
                {
                    if (Q.CastIfHitchanceEquals(enemy, HitChance.High, packetCast))
                    {
                        castWafter = Game.Time + Q.Delay;
                        return;
                    }
                }

                if (!Q.IsReady() && castWafter < Game.Time && W.IsReady() && !enemy.HasBuffOfType(BuffType.Poison) && (!harass || menu.Item("wHarass").GetValue<bool>()))
                {
                    W.CastIfHitchanceEquals(enemy, HitChance.High, packetCast);
                    return;
                }
            }
        }

        private static Obj_AI_Hero getTarget(float range)
        {
            Obj_AI_Hero target = null;

            if (Q.IsReady() || W.IsReady())
            {
                double minCasts = -1;
                foreach (var eTarget in GetEnemyList())
                {
                    if (eTarget.IsValidTarget(range))
                    {
                        if (dontUseQW2 > Game.ClockTime)
                            if (eTarget.NetworkId == dontUseQW)
                                continue;
                        
                        if (player.Distance(Q.GetPrediction(eTarget, true, Q.Range).CastPosition) > Q.Range) continue;

                        double casts = eTarget.Health / getEDmg(eTarget);
                        if (minCasts == -1 || minCasts > casts)
                        {
                            target = eTarget;
                            minCasts = casts;
                        }
                    }
                }
            }

            return target;
        }

        private static float GetPoisonBuffEndTime(Obj_AI_Base target)
        {
            var buffEndTime = target.Buffs.OrderByDescending(buff => buff.EndTime - Game.Time)
                    .Where(buff => buff.Type == BuffType.Poison)
                    .Select(buff => buff.EndTime)
                    .FirstOrDefault();
            return buffEndTime;
        }

        private static List<Obj_AI_Hero> GetEnemyList()
        {
            return ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValid).ToList();
        }

        private static double getQDmg(Obj_AI_Base target)
        {
            return player.CalcDamage(target, Damage.DamageType.Magical, (SpellManaReq(Q, 30, 16) + 0.18 * player.FlatMagicDamageMod));
        }

        private static double getEDmg(Obj_AI_Base target)
        {
            return player.CalcDamage(target, Damage.DamageType.Magical, (SpellManaReq(E, 55, 25) + 0.55 * player.FlatMagicDamageMod));
        }

        private static double getRDmg(Obj_AI_Base target)
        {
            return player.CalcDamage(target, Damage.DamageType.Magical, (SpellManaReq(R, 150, 100) + 0.5 * player.FlatMagicDamageMod));
        }

        static float SpellManaReq(Spell spell, float firstLevel, float step)
        {
            return (firstLevel + ((spell.Level - 1) * step));
        }

    }
}