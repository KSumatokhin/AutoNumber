using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
using Application = Bricscad.ApplicationServices.Application;

using AutoNumber.Models;

namespace AutoNumber
{
    public class AutoNumberCommand
    {
        [CommandMethod("AutoNumber")]
        public void AutoNumber()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // Собираем доступные теги из чертежа
                var availableTags = CollectAttributeTags(db);

                // Собираем имена блоков
                var availableBlocks = CollectBlockNames(db);

                // Создаем ViewModel
                var viewModel = new ViewModels.NumberingSettingsViewModel
                {
                    AvailableTags = new ObservableCollection<string>(availableTags),
                    AvailableBlocks = new ObservableCollection<string>(availableBlocks)
                };

                // Создаем и показываем диалог
                var dialog = new NumberingDialog(viewModel);

                // Устанавливаем владельца окна для AutoCAD
                //var wpfHandler = new WindowWrapper(Application.MainWindow.Handle);
                //System.Windows.Interop.WindowInteropHelper helper =
                //    new System.Windows.Interop.WindowInteropHelper(dialog);
                //helper.Owner = Application.MainWindow.Handle;

                // Обновляем предпросмотр при изменении настроек
                viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName != nameof(viewModel.PreviewItems))
                    {
                        UpdatePreview(doc, viewModel);
                    }
                };

                // Показываем диалог
                if (dialog.ShowDialog() == true)
                {
                    // Выполняем нумерацию
                    PerformNumbering(doc, viewModel);
                }
            }
            catch (System.Exception ex)
            {
                // Log detailed error information for debugging (in a production environment)
                // System.Diagnostics.Debug.WriteLine($"AutoNumber error: {ex}");
                
                // Show generic error message to user to prevent information disclosure
                ed.WriteMessage($"\nПроизошла ошибка при выполнении команды. Проверьте правильность ввода данных.");
            }
        }

        private List<string> CollectAttributeTags(Database db)
        {
            var tags = new HashSet<string>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId btrId in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                    // Пропускаем служебные блоки
                    if (btr.IsAnonymous || btr.IsLayout)
                        continue;

                    // Ищем атрибуты в определениях блоков
                    foreach (ObjectId id in btr)
                    {
                        if (id.ObjectClass.Name == "AcDbAttributeDefinition")
                        {
                            var attDef = (AttributeDefinition)tr.GetObject(id, OpenMode.ForRead);
                            tags.Add(attDef.Tag);
                        }
                    }
                }

                tr.Commit();
            }

            return tags.ToList();
        }

        private List<string> CollectBlockNames(Database db)
        {
            var blocks = new List<string>();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                foreach (ObjectId btrId in bt)
                {
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                    // Только неанонимные блоки, которые можно вставлять
                    if (!btr.IsAnonymous && !btr.IsLayout && !btr.IsDynamicBlock)
                    {
                        blocks.Add(btr.Name);
                    }
                }

                tr.Commit();
            }

            return blocks;
        }

        private void UpdatePreview(Document doc, ViewModels.NumberingSettingsViewModel settings)
        {
            // Здесь можно добавить логику предпросмотра
            // Например, показать первые 5 блоков, которые будут пронумерованы
        }

        private void PerformNumbering(Document doc, ViewModels.NumberingSettingsViewModel settings)
        {
            var ed = doc.Editor;

            using (var tr = doc.TransactionManager.StartTransaction())
            {
                try
                {
                    var blocksToNumber = GetBlocksToNumber(doc, settings, tr);
                    var sortedBlocks = SortBlocks(blocksToNumber, settings, tr);

                    int counter = settings.StartNumber;
                    int numbered = 0;

                    foreach (var br in sortedBlocks)
                    {
                        var bref = (BlockReference)tr.GetObject(br.ObjectId, OpenMode.ForWrite);

                        foreach (ObjectId attId in bref.AttributeCollection)
                        {
                            var att = (AttributeReference)tr.GetObject(attId, OpenMode.ForWrite);

                            if (att.Tag == settings.TagName)
                            {
                                string number = settings.Prefix + counter + settings.Suffix;
                                att.TextString = number;
                                numbered++;
                                break;
                            }
                        }

                        counter += settings.Increment;
                    }

                    tr.Commit();
                    ed.WriteMessage($"\nПронумеровано {numbered} блоков.");
                }
                catch (System.Exception ex)
                {
                    // Log detailed error information for debugging (in a production environment)
                    // System.Diagnostics.Debug.WriteLine($"AutoNumber numbering error: {ex}");
                    
                    ed.WriteMessage($"\nПроизошла ошибка при нумерации блоков. Проверьте правильность ввода данных.");
                    tr.Abort();
                }
            }
        }

        private List<BlockReference> GetBlocksToNumber(Document doc,
            ViewModels.NumberingSettingsViewModel settings, Transaction tr)
        {
            var result = new List<BlockReference>();
            var ed = doc.Editor;

            switch (settings.SelectedMode)
            {
                case Models.NumberingMode.SelectByWindow:
                    var selection = SelectBlocksByWindow(ed);
                    if (selection != null)
                    {
                        foreach (ObjectId id in selection)
                        {
                            var br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                            if (br != null && ShouldIncludeBlock(br, settings, tr))
                                result.Add(br);
                        }
                    }
                    break;

                case Models.NumberingMode.AllOnLayouts:
                    result = GetAllBlocksOnLayouts(doc, settings, tr);
                    break;

                case Models.NumberingMode.ByBlockName:
                    result = GetBlocksByName(doc, settings, tr);
                    break;
            }

            return result;
        }

        private ObjectId[] SelectBlocksByWindow(Editor ed)
        {
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "INSERT")
            });

            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\nВыберите блоки для нумерации:"
            };

            var result = ed.GetSelection(options, filter);
            return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : null;
        }

        private bool ShouldIncludeBlock(BlockReference br,
            ViewModels.NumberingSettingsViewModel settings, Transaction tr)
        {
            // Проверяем, нужно ли включать блок
            var owner = (BlockTableRecord)tr.GetObject(br.OwnerId, OpenMode.ForRead);
            var layout = (Layout)tr.GetObject(owner.LayoutId, OpenMode.ForRead);

            if (layout.LayoutName == "Model" && !settings.IncludeModelSpace)
                return false;

            return true;
        }

        private List<BlockReference> GetAllBlocksOnLayouts(Document doc,
            ViewModels.NumberingSettingsViewModel settings, Transaction tr)
        {
            var result = new List<BlockReference>();
            var db = doc.Database;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                if (btr.IsAnonymous || btr.IsLayout)
                    continue;

                var brefIds = btr.GetBlockReferenceIds(true, false);

                foreach (ObjectId id in brefIds)
                {
                    var br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);

                    if (ShouldIncludeBlock(br, settings, tr))
                        result.Add(br);
                }
            }

            return result;
        }

        private List<BlockReference> GetBlocksByName(Document doc,
            ViewModels.NumberingSettingsViewModel settings, Transaction tr)
        {
            var result = new List<BlockReference>();
            var db = doc.Database;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            // Sanitize user input for regex pattern to prevent ReDoS
            string sanitizedFilter = settings.BlockNameFilter.Replace("*", ".*");
            // Escape special regex characters to prevent injection
            string escapedPattern = System.Text.RegularExpressions.Regex.Escape(sanitizedFilter);
            // Convert escaped wildcards back to actual wildcards
            string finalPattern = escapedPattern.Replace("\\.\\*", ".*");
            
            var regex = new System.Text.RegularExpressions.Regex(finalPattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                if (btr.IsAnonymous || btr.IsLayout)
                    continue;

                if (regex.IsMatch(btr.Name))
                {
                    var brefIds = btr.GetBlockReferenceIds(true, false);

                    foreach (ObjectId id in brefIds)
                    {
                        var br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);

                        if (ShouldIncludeBlock(br, settings, tr))
                            result.Add(br);
                    }
                }
            }

            return result;
        }

        private List<BlockReference> SortBlocks(List<BlockReference> blocks,
            ViewModels.NumberingSettingsViewModel settings, Transaction tr)
        {
            IOrderedEnumerable<BlockReference> sorted = null;

            // Сначала применяем основную сортировку
            if (settings.SortByY)
            {
                sorted = settings.YDirection == Models.SortDirection.Ascending
                    ? blocks.OrderBy(b => b.Position.Y)
                    : blocks.OrderByDescending(b => b.Position.Y);
            }
            else if (settings.SortByX)
            {
                sorted = settings.XDirection == Models.SortDirection.Ascending
                    ? blocks.OrderBy(b => b.Position.X)
                    : blocks.OrderByDescending(b => b.Position.X);
            }

            // Затем применяем дополнительную сортировку
            if (sorted != null)
            {
                if (settings.SortByY && settings.SortByX)
                {
                    sorted = settings.XDirection == Models.SortDirection.Ascending
                        ? sorted.ThenBy(b => b.Position.X)
                        : sorted.ThenByDescending(b => b.Position.X);
                }
                else if (settings.SortByX && settings.SortByY)
                {
                    sorted = settings.YDirection == Models.SortDirection.Ascending
                        ? sorted.ThenBy(b => b.Position.Y)
                        : sorted.ThenByDescending(b => b.Position.Y);
                }

                return sorted.ToList();
            }

            // Если сортировка не задана, возвращаем как есть
            return blocks;
        }
        
        // Input validation methods
        private bool ValidateTagName(string tagName)
        {
            // Check for null or empty
            if (string.IsNullOrEmpty(tagName))
                return false;
                
            // Check length (prevent buffer overflow)
            if (tagName.Length > 255)
                return false;
                
            // Check for invalid characters
            char[] invalidChars = { '<', '>', '"', '|', '\0', '\n', '\r' };
            foreach (char c in invalidChars)
            {
                if (tagName.Contains(c))
                    return false;
            }
            
            return true;
        }
        
        private bool ValidatePrefixSuffix(string value)
        {
            // Allow null or empty
            if (string.IsNullOrEmpty(value))
                return true;
                
            // Check length
            if (value.Length > 100)
                return false;
                
            // Check for invalid characters
            char[] invalidChars = { '<', '>', '"', '|', '\0', '\n', '\r' };
            foreach (char c in invalidChars)
            {
                if (value.Contains(c))
                    return false;
            }
            
            return true;
        }
        
        private bool ValidateBlockNameFilter(string filter)
        {
            // Check for null or empty
            if (string.IsNullOrEmpty(filter))
                return false;
                
            // Check length
            if (filter.Length > 255)
                return false;
                
            // Check for invalid characters that could cause issues
            char[] invalidChars = { '<', '>', '"', '|', '\0', '\n', '\r' };
            foreach (char c in invalidChars)
            {
                if (filter.Contains(c))
                    return false;
            }
            
            return true;
        }
    }
}