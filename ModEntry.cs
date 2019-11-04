using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.TerrainFeatures;

namespace CropTimerTooltip
{
    public class CropTimer : Mod
    {
        private string crop_name;
        private int current_locale_number;
        private string current_location;
        private string harvestTimeLeft;
        private bool is_button_pressed;
        private bool is_subscribed;
        private bool mouse_hover;
        private Vector2 prev_cursor_tile = new Vector2(-1f, -1f);
        private SpriteFont text_box_font = Game1.smallFont;
        private int text_box_height = 60;

        private readonly HashSet<int> wild_seed_indices = new HashSet<int>
            {16, 18, 20, 22, 396, 398, 402, 404, 406, 408, 410, 412, 414, 416, 418};

        private bool work_everywhere;

        public override void Entry(IModHelper helper)
        {
            var sclModConfig = helper.ReadConfig<SCLModConfig>();
            mouse_hover = sclModConfig.mouse_hover;
            work_everywhere = sclModConfig.work_everywhere;
            Helper.Events.GameLoop.SaveLoaded += GameLoop_SaveLoaded;
            Helper.Events.Player.Warped += PlayerEvents_Warped;
        }

        private void GameLoop_SaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            SetCurrentLocaleNumber(LocalizedContentManager.CurrentLanguageCode);
        }

        private void PlayerEvents_Warped(object sender, WarpedEventArgs e)
        {
            if (e.NewLocation == null || !e.IsLocalPlayer)
                return;
            if (OnCropLocation(e.NewLocation.Name))
            {
                current_location = e.NewLocation.Name;
                if (is_subscribed)
                    return;
                if (!mouse_hover)
                {
                    Helper.Events.Input.ButtonPressed += InputEvents_ButtonPressed;
                    Helper.Events.Input.ButtonReleased += InputEvents_ButtonReleased;
                }

                Helper.Events.Input.CursorMoved += InputEvents_CursorMoved;
                Helper.Events.Display.RenderedHud += GraphicsEvents_OnPostRenderHudEvent;
                is_subscribed = true;
            }
            else
            {
                if (OnCropLocation(e.NewLocation.Name) || !is_subscribed)
                    return;
                current_location = null;
                if (!mouse_hover)
                {
                    Helper.Events.Input.ButtonPressed -= InputEvents_ButtonPressed;
                    Helper.Events.Input.ButtonReleased -= InputEvents_ButtonReleased;
                }

                Helper.Events.Input.CursorMoved -= InputEvents_CursorMoved;
                Helper.Events.Display.RenderedHud -= GraphicsEvents_OnPostRenderHudEvent;
                ClearCursorState();
                is_subscribed = false;
            }
        }

        private void InputEvents_CursorMoved(object sender, CursorMovedEventArgs e)
        {
            if (!Context.IsPlayerFree || current_location == null || !mouse_hover && !is_button_pressed)
                return;
            FindCrop();
        }

        private void InputEvents_ButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsPlayerFree || current_location == null ||
                e.Button != SButton.ControllerA && e.Button != SButton.MouseRight)
                return;
            FindCrop();
            is_button_pressed = true;
        }

        private void InputEvents_ButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (e.Button != SButton.ControllerA && e.Button != SButton.MouseRight)
                return;
            ClearCursorState();
            is_button_pressed = false;
        }

        private void GraphicsEvents_OnPostRenderHudEvent(object sender, EventArgs e)
        {
            if (crop_name == null || !Context.IsPlayerFree)
                return;
            var num1 = 64;
            Vector2 textboxSize;
            var spriteBatch = Game1.spriteBatch;
            if (harvestTimeLeft != "")
                textboxSize = text_box_font.MeasureString(harvestTimeLeft);
            else
                textboxSize = text_box_font.MeasureString(crop_name);

            var num2 = num1 / 2;
            var width = (int) (textboxSize.X + num2);
            var height = Math.Max(text_box_height, (int) textboxSize.Y);
            var x = Game1.getOldMouseX() + num2;
            var y = Game1.getOldMouseY() + num2;
            if (x + width > Game1.viewport.Width)
            {
                x = Game1.viewport.Width - width;
                y += num2;
            }

            if (y + height > Game1.viewport.Height)
            {
                x += num2;
                y = Game1.viewport.Height - height;
            }

            if (harvestTimeLeft != "")
            {
                IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y,
                    width, (int) (height * 1.5), Color.White);
                Utility.drawTextWithShadow(spriteBatch, crop_name, text_box_font,
                    new Vector2(x + num1 / 4, y + num1 / 4), Game1.textColor);
                Utility.drawTextWithShadow(spriteBatch, harvestTimeLeft, text_box_font,
                    new Vector2(x + num1 / 4, y + num1 / 4 + (int) (height * 0.5)), Game1.textColor);
            }
            else
            {
                IClickableMenu.drawTextureBox(spriteBatch, Game1.menuTexture, new Rectangle(0, 256, 60, 60), x, y,
                    width, height, Color.White);
                Utility.drawTextWithShadow(spriteBatch, crop_name, text_box_font,
                    new Vector2(x + num1 / 4, y + num1 / 4), Game1.textColor);
            }
        }

        private void FindCrop()
        {
            if (prev_cursor_tile == Game1.currentCursorTile)
                return;
            prev_cursor_tile = Game1.currentCursorTile;
            var terrainFeature = (TerrainFeature) null;
            crop_name = null;
            var result = -1;
            var objectAtTile = Game1.getLocationFromName(current_location)
                .getObjectAtTile((int) prev_cursor_tile.X, (int) prev_cursor_tile.Y);


            if (objectAtTile != null && objectAtTile.Type.Equals("Basic") &&
                int.TryParse(objectAtTile.ParentSheetIndex.ToString(), out result) &&
                wild_seed_indices.Contains(result))
            {
                crop_name = CropIndexToName(result);
            }
            else
            {
                if (!Game1.getLocationFromName(current_location).terrainFeatures
                    .TryGetValue(prev_cursor_tile, out terrainFeature))
                    return;
                if (terrainFeature is HoeDirt && (terrainFeature as HoeDirt).crop != null)
                {
                    var crop = (terrainFeature as HoeDirt).crop;
                    harvestTimeLeft = getHarvestTime(crop);
                    var some_index = crop.indexOfHarvest.Value;
                    if (crop.isWildSeedCrop())
                        some_index = crop.whichForageCrop.Value;
                    crop_name = CropIndexToName(some_index);
                }
                else if (terrainFeature is FruitTree && terrainFeature is FruitTree)
                {
                    crop_name = CropIndexToName((terrainFeature as FruitTree).indexOfFruit.Value);
                }
                else if (terrainFeature is Tree && terrainFeature is Tree)
                {
                    var num = (terrainFeature as Tree).treeType.Value;
                    if (num == 4)
                        num = 1;
                    else if (num == 5)
                        num = 2;
                    switch (num - 1)
                    {
                        case 0:
                            crop_name =
                                "Oak Tree/Eiche/Roble/Carvalho/Дуб/樫の木/橡树/Chêne/Tölgyfa/Quercia/떡갈 나무/Meşe Ağacı";
                            break;
                        case 1:
                            crop_name =
                                "Maple Tree/Ahornbaum/Arce/Ácer/Клен/カエデ/枫树/Érable/Juharfa/Acero/단풍 나무/Akçaağaç";
                            break;
                        case 2:
                            crop_name = "Pine Tree/Kiefer/Pino/Pinheiro/Cосну/松の木/松树/Pin/Fenyőfa/Pino/소나무/Çam Ağacı";
                            break;
                        case 5:
                            crop_name =
                                "Coconut Tree/Kokosnussbaum/Cocotero/Coqueiro/Кокос/ココナッツの木/椰子树/Cocotier/Kókuszfa/Palma da Cocco/코코넛 나무/Hindistan Cevizi Ağacı";
                            break;
                        case 6:
                            crop_name =
                                "Mushroom Tree/Pilzbaum/Árbol de Setas/Árvore de Cogumelo/Грибное Дерево/キノコの木/蘑菇树/Champignon arbre/Gombafa/Fungo Albero/버섯 나무/Mantar Ağacı";
                            break;
                        default:
                            crop_name = null;
                            break;
                    }

                    if (crop_name == null)
                        return;
                    crop_name = crop_name.Split('/')[current_locale_number];
                }
                else
                {
                    crop_name = null;
                }
            }
        }

        private string getHarvestTime(Crop crop)
        {
            var cropName = new ArrayList();
            string str;
            Game1.objectInformation.TryGetValue(crop.indexOfHarvest.Value, out str);
            cropName.Add(str.Split('/')[4]);

            var currentGrowthPhase = Math.Min(crop.phaseDays.Count - 1, crop.currentPhase.Value);
            var dayOfCurrentPhase = Math.Max(crop.dayOfCurrentPhase.Value, 0);
            var totalDaysToGrowCrop = 0;
            var previousPhaseDays = 0;
            for (var i = 0; i < crop.phaseDays.Count - 1; ++i)
            {
                totalDaysToGrowCrop += crop.phaseDays[i];
                if (i < currentGrowthPhase && currentGrowthPhase > 0)
                    previousPhaseDays += crop.phaseDays[i];
            }

            var daysGrown = previousPhaseDays + dayOfCurrentPhase;
            var daysLeftToGrow = totalDaysToGrowCrop - daysGrown;
            var daysUntilSeasonEnds = 28 - Game1.dayOfMonth;
            if (crop.fullyGrown.Value && daysLeftToGrow < 0)
                daysLeftToGrow = Math.Abs(daysLeftToGrow);
            else if (daysLeftToGrow < 0)
                daysLeftToGrow = 0;
            //Monitor.Log(string.Format("End of the Month: {0} ... Days Left To Grow: {1} ... can harvest: {2} ", daysUntilSeasonEnds, daysLeftToGrow, daysLeftToGrow <= daysUntilSeasonEnds), (LogLevel)1);
            if (crop.dead.Value)
                cropName.Add("Is dead!");
            else if (daysLeftToGrow == 0)
                cropName.Add("Ready to harvest!");
            else
                cropName.Add(string.Format("Harvest in {0} {1}", daysLeftToGrow, daysLeftToGrow != 1 ? "days" : "day"));

            return string.Format("Harvest in {0} {1}", daysLeftToGrow, daysLeftToGrow != 1 ? "days" : "day");
        }

        private string CropIndexToName(int some_index)
        {
            string str = null;
            if (Game1.objectInformation.TryGetValue(some_index, out crop_name)) str = crop_name.Split('/')[4];

            return str;
        }

        private bool OnCropLocation(string new_location)
        {
            return work_everywhere || new_location.Equals("Farm") || new_location.Equals("Greenhouse");
        }

        private void ClearCursorState()
        {
            prev_cursor_tile.X = -1f;
            prev_cursor_tile.Y = -1f;
            crop_name = null;
            harvestTimeLeft = "";
        }

        private void SetCurrentLocaleNumber(LocalizedContentManager.LanguageCode current_code)
        {
            text_box_font = Game1.smallFont;
            text_box_height = 60;
            switch (current_code)
            {
                case LocalizedContentManager.LanguageCode.en:
                    current_locale_number = 0;
                    break;
                case LocalizedContentManager.LanguageCode.ja:
                    current_locale_number = 5;
                    break;
                case LocalizedContentManager.LanguageCode.ru:
                    current_locale_number = 4;
                    break;
                case LocalizedContentManager.LanguageCode.zh:
                    current_locale_number = 6;
                    text_box_font = Game1.dialogueFont;
                    text_box_height = 70;
                    break;
                case LocalizedContentManager.LanguageCode.pt:
                    current_locale_number = 3;
                    break;
                case LocalizedContentManager.LanguageCode.es:
                    current_locale_number = 2;
                    break;
                case LocalizedContentManager.LanguageCode.de:
                    current_locale_number = 1;
                    break;
                case LocalizedContentManager.LanguageCode.fr:
                    current_locale_number = 7;
                    break;
                case LocalizedContentManager.LanguageCode.ko:
                    current_locale_number = 10;
                    text_box_height = 70;
                    break;
                case LocalizedContentManager.LanguageCode.it:
                    current_locale_number = 9;
                    break;
                case LocalizedContentManager.LanguageCode.tr:
                    current_locale_number = 11;
                    break;
                case LocalizedContentManager.LanguageCode.hu:
                    current_locale_number = 8;
                    break;
                default:
                    current_locale_number = 0;
                    break;
            }
        }
    }
}