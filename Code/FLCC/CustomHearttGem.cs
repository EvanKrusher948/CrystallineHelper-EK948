using Celeste;
using Celeste.Mod;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace vitmod
{
	[CustomEntity("vitellary/customheartt")]
    public class CustomHearttGem : Entity
    {
		public enum SpriteType
		{
			Blue,
			Red,
			Gold,
			Custom,
			Core,
			CoreInverted,
			Random
		}

		public static ParticleType P_Shatter = new ParticleType
		{
			Color = Color.Blue,
			Color2 = Color.White,
			ColorMode = ParticleType.ColorModes.Blink,
			FadeMode = ParticleType.FadeModes.Late,
			LifeMin = 0.25f,
			LifeMax = 0.4f,
			Size = 1f,
			Direction = 0f,
			DirectionRange = (float)Math.PI*2f,
			SpeedMin = 50f,
			SpeedMax = 80f,
			SpeedMultiplier = 0.005f,
		};

		public Wiggler ScaleWiggler;

		private bool slowdown;
		private float respawnTime;
		private string poemId;
        private bool smlHbx;
        private SpriteType spriteType;
		private string spritePath;
		private string spriteColor;
		private bool bully;
		private bool hasLight;
        private bool hasDust;
        private bool switchCoreMode;
		private bool isStatic;
        private bool cutscene;

		private EntityID entityID;
		private Sprite sprite;
		private Sprite coldSprite;
		private Sprite white;
		private Sprite outline;
		private ParticleType shineParticle;
		private ParticleType breakParticle;
		private Wiggler moveWiggler;
		private Vector2 moveWiggleDir;
		private BloomPoint bloom;
		private VertexLight light;
		private Poem poem;
		private float timer;
		private bool collected;
		private bool collecting;
		private bool autoPulse;
		private float bounceSfxDelay;
		private float respawnTimer;
		private int dashCount;
        private float staminaExt;
        private SoundEmitter sfx;
		private List<InvisibleBarrier> walls;
		private HoldableCollider holdableCollider;

        private const string DEFAULT_PATH = "CrystallineHelper/FLCC/heartGemColorable";

		public CustomHearttGem(EntityData data, Vector2 offset) : base(data.Position + offset)
		{
			entityID = new EntityID(data.Level.Name, data.ID); //regular stuff
			respawnTime = data.Float("respawnTime", -1f);
			dashCount = data.Int("dashCount", 1);
            staminaExt = data.Float("staminaE", 0f);
            smlHbx = data.Bool("smallHitbox", false); //toggles small hitbox used by mini hearts and summit gems
            //Depth = data.Int("hepth", 0);

            spriteType = data.Enum<SpriteType>("type"); //sprite effects
			spritePath = $"collectables/{data.Attr("path", DEFAULT_PATH)}/";
            // handle legacy FLCC path
            if (spritePath == "collectables/heartGemColorable/") {
                spritePath = $"collectables/{DEFAULT_PATH}/";
            }
			spriteColor = data.Attr("color", "ffffff");
			hasLight = data.Bool("light", true);
            hasDust = data.Bool("dust", true);
            isStatic = data.Bool("static");

            bully = data.Bool("bully");
            switchCoreMode = data.Bool("switchCoreMode");
            slowdown = data.Bool("slowdown");
            cutscene = data.Bool("poemCutscene");
            poemId = data.Attr("poemId");

            autoPulse = true;
			walls = new List<InvisibleBarrier>();
            //Add(holdableCollider = new HoldableCollider(OnHoldable));
            Add(new MirrorReflection());
		}

		public override void Awake(Scene scene)
		{
			base.Awake(scene); //initial loading
			Level level = base.Scene as Level;
			if (level.Session.DoNotLoad.Contains(entityID))
			{
				RemoveSelf();
				return;
			}
            //sprite & visual loading
            if (spriteType == SpriteType.Random)
            {
                spriteType = Calc.Random.Choose(SpriteType.Blue, SpriteType.Red, SpriteType.Gold);
            }
            Color value = Color.White;
            switch (spriteType)
            {
                default:
                    Add(sprite = CreateSprite(""));
                    sprite.SetColor(Calc.HexToColor(spriteColor));
                    value = Calc.HexToColor(spriteColor);
                    if (GFX.Game.Has($"{spritePath}outline00"))
                    {
                        Add(outline = CreateSprite("outline"));
                    }
                    shineParticle = new ParticleType(HeartGem.P_BlueShine)
                    {
                        Color = value
                    };
                    break;
                case SpriteType.Blue:
                    Add(sprite = GFX.SpriteBank.Create("heartgem0"));
                    value = Color.Aqua;
                    shineParticle = HeartGem.P_BlueShine;
                    break;
                case SpriteType.Red:
                    Add(sprite = GFX.SpriteBank.Create("heartgem1"));
                    value = Color.Red;
                    shineParticle = HeartGem.P_RedShine;
                    break;
                case SpriteType.Gold:
                    Add(sprite = GFX.SpriteBank.Create("heartgem2"));
                    value = Color.Gold;
                    shineParticle = HeartGem.P_GoldShine;
                    break;
                case SpriteType.Core:
                    Add(sprite = GFX.SpriteBank.Create("heartgem1"));
                    Add(coldSprite = GFX.SpriteBank.Create("heartgem0"));
                    if (level.CoreMode == Session.CoreModes.Cold)
                    {
                        sprite.Visible = false;
                        value = Color.Aqua;
                        shineParticle = HeartGem.P_BlueShine;
                    }
                    else
                    {
                        coldSprite.Visible = false;
                        value = Color.Red;
                        shineParticle = HeartGem.P_RedShine;
                    }
                    break;
                case SpriteType.CoreInverted:
                    Add(sprite = GFX.SpriteBank.Create("heartgem0"));
                    Add(coldSprite = GFX.SpriteBank.Create("heartgem1"));
                    if (level.CoreMode == Session.CoreModes.Cold)
                    {
                        sprite.Visible = false;
                        value = Color.Red;
                        shineParticle = HeartGem.P_RedShine;
                    }
                    else
                    {
                        coldSprite.Visible = false;
                        value = Color.Aqua;
                        shineParticle = HeartGem.P_BlueShine;
                    }
                    break;
            }
			sprite.Play("spin"); //spin animation
			if (coldSprite != null && coldSprite.Visible)
				coldSprite.Play("spin");
			sprite.OnLoop = delegate (string anim)
			{
				if (Visible && anim == "spin" && autoPulse)
				{
					Audio.Play("event:/game/general/crystalheart_pulse", Position);
					ScaleWiggler.Start();
					(base.Scene as Level).Displacement.AddBurst(Position, 0.35f, 8f, 48f, 0.25f);
				}
			};
            value = Color.Lerp(value, Color.White, 0.5f);
            if (hasLight)
            {
                Add(light = new VertexLight(value, 1f, 32, 64));
                //Add(light = new VertexLight(value, hasLight ? 1f : 0f, 32, 64));
                Add(bloom = new BloomPoint(0.75f, 16f));
            }
            breakParticle = new ParticleType(P_Shatter)
            {
                Color = shineParticle.Color
            };
            //hitbox
            if (smlHbx)
            {
                Collider = new Hitbox(12f, 12f, -6f, -6f);
            }
            else
            {
                Collider = new Hitbox(16f, 16f, -8f, -8f);
            }
			Add(new PlayerCollider(OnPlayer));
            if (!isStatic)
            {
                Add(ScaleWiggler = Wiggler.Create(0.5f, 4f, delegate (float f)
                {
                    sprite.Scale = Vector2.One * (1f + f * 0.25f);
                }));
                moveWiggler = Wiggler.Create(0.8f, 2f);
                moveWiggler.StartZero = true;
                Add(moveWiggler);
            }
		}

		private Sprite CreateSprite(string path)
		{
			var sprite = new Sprite(GFX.SpriteBank.Atlas, spritePath);
			sprite.AddLoop("idle", path, 0f, new int[] { 0 });
			sprite.AddLoop("spin", path, 0.1f, Calc.ReadCSVIntWithTricks("0*10,1-13"));
			sprite.AddLoop("fastspin", path, 0.1f);
			sprite.CenterOrigin();
			sprite.Justify = new Vector2(0.5f, 0.5f);
			sprite.Play("idle");
			return sprite;
		}

		public override void Update()
		{
            if (bounceSfxDelay > 0f)
            {
                bounceSfxDelay -= Engine.DeltaTime;
            }
            if (collected) // handles respawn logic
            {
                respawnTimer -= Engine.DeltaTime;
                if (respawnTimer <= 0f)
                {
                    respawnTimer = 0f;
                    collected = false;
                    Collidable = true;
                    Visible = true;
                    Audio.Play("event:/game/general/diamond_return", Position);
                    if (hasLight)
                    {
                        bloom.Alpha = 0.75f;
                        light.Alpha = 1f;
                    }
                    if (!isStatic)
                    {
                        ScaleWiggler.Start();
                    }
                }
            }

			if (spriteType == SpriteType.Core || spriteType == SpriteType.CoreInverted) //handles core sprites
			{
				if (SceneAs<Level>().CoreMode == Session.CoreModes.Cold)
				{
					sprite.Visible = false;
					coldSprite.Visible = true;
					shineParticle = (spriteType == SpriteType.Core ? HeartGem.P_BlueShine : HeartGem.P_RedShine);
				}
				else
				{
					sprite.Visible = true;
					coldSprite.Visible = false;
					shineParticle = (spriteType == SpriteType.Core ? HeartGem.P_RedShine : HeartGem.P_BlueShine);
				}
			}

			if (collecting && (Scene.Tracker.GetEntity<Player>()?.Dead ?? true)) //basically makes sure the collectroutine isnt a complete ass to go through when dying
			{
				EndCutscene();
			}

			base.Update();

            if (!isStatic) //wiggle sprite if not static
            {
                timer += Engine.DeltaTime;
                sprite.Position = Vector2.UnitY * (float)Math.Sin(timer * 2f) * 2f + moveWiggleDir * moveWiggler.Value * -8f;
                if (timer >= (float)Math.PI)
                {
                    timer -= (float)Math.PI;
                }
            }
			var sprites = new List<Sprite>(); //basically registers any other sprite the heart needs & gives them the same variables as the original
			if (coldSprite != null)
				sprites.Add(coldSprite);
			if (outline != null)
				sprites.Add(outline);
			if (white != null)
				sprites.Add(white);
			foreach (var other in sprites)
			{
				other.Position = sprite.Position;
				other.Scale = sprite.Scale;
				if (other.CurrentAnimationID != sprite.CurrentAnimationID)
				{
					other.Play(sprite.CurrentAnimationID);
				}
				other.SetAnimationFrame(sprite.CurrentAnimationFrame);
			}

			if (Visible && hasDust && Scene.OnInterval(0.1f)) //emit particles
			{
				SceneAs<Level>().Particles.Emit(shineParticle, 1, base.Center, Vector2.One * 8f);
			}
		}

		public void OnHoldable(Holdable h)
		{
			Player entity = Scene.Tracker.GetEntity<Player>();
			if (!collected && entity != null && h.Dangerous(holdableCollider))
			{
				Collect(entity, h.GetSpeed().Angle());
			} //add holdable bully, should be easy bc seekers have a function that bounces theos off
            //hitSeeker and Swat in theo crystal, Swat in player
            //will need to also rework the normal bully to allow both as options, preferrably a toggle
            //make sure to load theo hitbox no matter what due to this rework
		}

		public void OnPlayer(Player player)
		{
			if (collected || (base.Scene as Level).Frozen)
			{
				return;
			}
			if (player.DashAttacking && !bully)
			{
				Collect(player, player.Speed.Angle());
				return;
			}
			if (bounceSfxDelay <= 0f)
			{
                if (bully)
                {
                    Audio.Play("event:/new_content/game/10_farewell/fakeheart_bounce", Position);
                }
                else
                {
                    Audio.Play("event:/game/general/crystalheart_bounce", Position);
                }
				bounceSfxDelay = 0.1f;
			}
            var dashes = Math.Max(player.Dashes, dashCount); //note: custom pointbounce may be good but idk how to modify player
            var stamina = player.Stamina + staminaExt;
            player.PointBounce(base.Center);
            player.Dashes = dashes;
            player.Stamina = stamina;
            if (!isStatic)
            {
                moveWiggler.Start();
                ScaleWiggler.Start();
                moveWiggleDir = (base.Center - player.Center).SafeNormalize(Vector2.UnitY);
            }
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
		}

		private void Collect(Player player, float angle)
		{
			if (Collidable)
			{
                Level level = SceneAs<Level>();
                player.Dashes = Math.Max(player.Dashes, dashCount);
                if (switchCoreMode)
				{
                    level.CoreMode = (level.CoreMode == Session.CoreModes.Cold ? Session.CoreModes.Hot : Session.CoreModes.Cold);
                }
                
				if (slowdown)
				{
					Scene.Tracker.GetEntity<AngryOshiro>()?.StopControllingTime();
					Coroutine coroutine = new Coroutine(CollectRoutine(player));
					coroutine.UseRawDeltaTime = true;
					Add(coroutine);
					collecting = true;
				}
				else
				{
					Celeste.Celeste.Freeze(0.05f);
                    Audio.Play("event:/game/general/diamond_touch", Position);
                    if (hasLight)
					{
						light.Alpha = 0f;
						bloom.Alpha = 0f;
					}
                    if (hasDust)
                    {
                        SceneAs<Level>().Particles.Emit(breakParticle, 8, Center, Vector2.One * 8f);
                        SlashFx.Burst(Position, angle);
                    }
					SceneAs<Level>().Shake(0.3f);
					Visible = false;
					PostCollect();
				}
				Collidable = false;
			}
		}

		private IEnumerator CollectRoutine(Player player)
		{
			Level level = Scene as Level;
			AreaKey area = level.Session.Area;
			level.CanRetry = false;
			/*if (endLevel)
			{
				Audio.SetMusic(null);
				Audio.SetAmbience(null);
				List<IStrawberry> list = new List<IStrawberry>();
				ReadOnlyCollection<Type> berryTypes = StrawberryRegistry.GetBerryTypes();
				foreach (Follower follower in player.Leader.Followers)
				{
					if (berryTypes.Contains(follower.Entity.GetType()) && follower.Entity is IStrawberry)
					{
						list.Add(follower.Entity as IStrawberry);
					}
				}
				foreach (IStrawberry item in list)
				{
					item.OnCollect();
				}
			}*/
            //note: this string of code is being kept incase i need nay functions, although ilspy is a thing
			string sfxEvent;
			if (area.Mode == AreaMode.BSide)
			{
				sfxEvent = "event:/game/general/crystalheart_red_get";
			}
			else if (area.Mode == AreaMode.CSide)
			{
				sfxEvent = "event:/game/general/crystalheart_gold_get";
			}
			else
			{
				sfxEvent = "event:/game/general/crystalheart_blue_get";
			}

			sfx = SoundEmitter.Play(sfxEvent, this);
			Add(new LevelEndingHook(delegate
			{
				sfx.Source.Stop();
			})); //this hook basically makes the sound stop when exiting or clearing a level
			walls.Add(new InvisibleBarrier(new Vector2(level.Bounds.Right, level.Bounds.Top), 8f, level.Bounds.Height));
			walls.Add(new InvisibleBarrier(new Vector2(level.Bounds.Left - 8, level.Bounds.Top), 8f, level.Bounds.Height));
			walls.Add(new InvisibleBarrier(new Vector2(level.Bounds.Left, level.Bounds.Top - 8), level.Bounds.Width, 8f));
			foreach (InvisibleBarrier wall in walls)
			{
				Scene.Add(wall);
			}
			if (spriteType == SpriteType.Custom && GFX.Game.Has($"{spritePath}white00"))
			{
				Add(white = CreateSprite("white"));
			}
			else
			{
				Add(white = GFX.SpriteBank.Create("heartGemWhite"));
			}
			Depth = -2000000; //note: yield return null means wait a frame, also this never gets reset
			yield return null;
			Celeste.Celeste.Freeze(0.05f);
			yield return null;
			Engine.TimeRate = 0.5f; //note: Engine.TimeRate is basically game speed since it goes into deltaTime
			player.Depth = -2000000;
			for (int i = 0; i < 10; i++)
			{
				Scene.Add(new AbsorbOrb(Position));
			}
			level.Shake();
			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			level.Flash(Color.White);
			level.FormationBackdrop.Display = true;
			level.FormationBackdrop.Alpha = 1f;
			light.Alpha = (bloom.Alpha = 0f);
			Visible = false;
			for (float t3 = 0f; t3 < 2f; t3 += Engine.RawDeltaTime) //lasts 2 seconds, each frame here slows down the game by 1/4 of a frame. totals into slowing the game down by 30f = 0.5 sec
			{
				Engine.TimeRate = Calc.Approach(Engine.TimeRate, 0f, Engine.RawDeltaTime * 0.25f);
				yield return null;
			}
			yield return null;
			if (player.Dead)
			{
				yield return 100f;
			}
            if (cutscene) {
                Engine.TimeRate = 1f;
                Tag = Tags.FrozenUpdate;
                level.Frozen = true;

                string poemText = null;
                if (!string.IsNullOrEmpty(poemId)) {
                    poemText = Dialog.Clean("poem_" + poemId);
                }
                int heartIndex;
                switch (spriteType) {
                    default:
                        heartIndex = 3;
                        break;
                    case SpriteType.Blue:
                        heartIndex = 0;
                        break;
                    case SpriteType.Red:
                        heartIndex = 1;
                        break;
                    case SpriteType.Gold:
                        heartIndex = 2;
                        break;
                    case SpriteType.Core:
                        heartIndex = (level.CoreMode == Session.CoreModes.Cold ? 0 : 1);
                        break;
                    case SpriteType.CoreInverted:
                        heartIndex = (level.CoreMode == Session.CoreModes.Cold ? 1 : 0);
                        break;
                }
                poem = new Poem(poemText, heartIndex, 0.8f);
                poem.Alpha = 0f;
                Scene.Add(poem);
                if (spriteType == SpriteType.Custom) {
                    poem.Heart.SetColor(Calc.HexToColor(spriteColor));
                }
                for (float t2 = 0f; t2 < 1f; t2 += Engine.RawDeltaTime) {
                    poem.Alpha = Ease.CubeOut(t2);
                    yield return null;
                }
                while (!Input.MenuConfirm.Pressed && !Input.MenuCancel.Pressed) {
                    yield return null;
                }
            }
            sfx.Source.Param("end", 1f);
            Depth = 0;
            player.Depth = 0;
            if (!cutscene)
            {
                Audio.Play("event:/game/general/diamond_return", Position);
                while (Engine.TimeRate < 1f)
                {
                    Engine.TimeRate = Calc.Approach(Engine.TimeRate, 1f, Engine.RawDeltaTime * 2f);
                    yield return null;
                }
            }
            else
            {
                for (float t = 0f; t < 1f; t += Engine.RawDeltaTime * 2f)
                {
                    poem.Alpha = Ease.CubeIn(1f - t);
                    yield return null;
                }
            }
            EndCutscene();
            PostCollect(); //things could be optimized at the end here to not repeat the same action 300000 times
        }

		private void EndCutscene()
		{
			Level level = base.Scene as Level;
			level.Frozen = false;
			level.CanRetry = true;
			level.FormationBackdrop.Display = false;
			Engine.TimeRate = 1f;
			if (poem != null)
			{
				poem.RemoveSelf();
			}
			foreach (InvisibleBarrier wall in walls)
			{
				wall.RemoveSelf();
			}
			collecting = false;
		}

		private void PostCollect()
		{
			collected = true;
			if (respawnTime >= 0f)
			{
				respawnTimer = respawnTime;
				Remove(white);
				white = null;
				Visible = false;
			}
			else
			{
				RemoveSelf();
			}
		}
	}
}
