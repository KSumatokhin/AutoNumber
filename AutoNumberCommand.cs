using AutoNumber.Models;
using AutoNumber.ViewModels;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Interop;
using Teigha.DatabaseServices;
using Teigha.Runtime;
using Application = Bricscad.ApplicationServices.Application;

namespace AutoNumber
{
    public class AutoNumberCommand : IExtensionApplication
    {
        private const double RowTolerance = 0.1;

        public void Initialize()
        {
            var document = Application.DocumentManager.MdiActiveDocument;
            document?.Editor.WriteMessage(
                "\nAutoNumber загружен. Команды: KS-RENUMATT, AutoNumber.");
        }

        public void Terminate()
        {
        }

        [CommandMethod("KS-RENUMATT")]
        [CommandMethod("AutoNumber")]
        public void AutoNumber()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var editor = doc.Editor;

            try
            {
                ObjectId sampleId = PromptForSample(editor);
                if (sampleId.IsNull)
                    return;

                string blockName;
                List<string> tags;
                using (var transaction = doc.TransactionManager.StartTransaction())
                {
                    var sample = (BlockReference)transaction.GetObject(sampleId, OpenMode.ForRead);
                    blockName = GetEffectiveBlockName(sample, transaction);
                    tags = GetAttributeTags(sample, transaction);
                }

                if (tags.Count == 0)
                {
                    editor.WriteMessage("\nУ выбранного блока нет редактируемых атрибутов.");
                    return;
                }

                var settings = new NumberingSettingsViewModel
                {
                    BlockName = blockName,
                    AvailableTags = new ObservableCollection<string>(tags),
                    TagName = tags.FirstOrDefault(t => string.Equals(t, "NUM", StringComparison.OrdinalIgnoreCase)) ?? tags[0]
                };

                var dialog = new NumberingDialog(settings);
                new WindowInteropHelper(dialog).Owner = Application.MainWindow.Handle;
                if (dialog.ShowDialog() != true)
                    return;

                ObjectId[] selectedIds = null;
                if (settings.Scope == NumberingScope.SelectedObjects)
                {
                    selectedIds = PromptForObjects(editor);
                    if (selectedIds == null)
                        return;
                }

                NumberBlocks(doc, settings, blockName, selectedIds);
            }
            catch (System.Exception exception)
            {
                editor.WriteMessage("\nОшибка AutoNumber: " + exception.Message);
            }
        }

        private static ObjectId PromptForSample(Editor editor)
        {
            var options = new PromptEntityOptions("\nВыберите блок-образец: ");
            options.SetRejectMessage("\nНужно выбрать блок.");
            options.AddAllowedClass(typeof(BlockReference), true);
            var result = editor.GetEntity(options);
            return result.Status == PromptStatus.OK ? result.ObjectId : ObjectId.Null;
        }

        private static ObjectId[] PromptForObjects(Editor editor)
        {
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "INSERT") });
            var options = new PromptSelectionOptions { MessageForAdding = "\nВыберите блоки для нумерации: " };
            var result = editor.GetSelection(options, filter);
            return result.Status == PromptStatus.OK ? result.Value.GetObjectIds() : null;
        }

        private static List<string> GetAttributeTags(BlockReference block, Transaction transaction)
        {
            var result = new List<string>();
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                if (attribute != null && !result.Contains(attribute.Tag, StringComparer.OrdinalIgnoreCase))
                    result.Add(attribute.Tag);
            }
            return result.OrderBy(tag => tag).ToList();
        }

        private static string GetEffectiveBlockName(BlockReference block, Transaction transaction)
        {
            ObjectId definitionId = block.IsDynamicBlock ? block.DynamicBlockTableRecord : block.BlockTableRecord;
            var definition = (BlockTableRecord)transaction.GetObject(definitionId, OpenMode.ForRead);
            return definition.Name;
        }

        private static bool HasAttribute(BlockReference block, string tag, Transaction transaction)
        {
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                if (attribute != null && string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool TrySetAttribute(BlockReference block, string tag, string value, Transaction transaction)
        {
            foreach (ObjectId attributeId in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(attributeId, OpenMode.ForRead) as AttributeReference;
                if (attribute == null || !string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    continue;

                attribute.UpgradeOpen();
                attribute.TextString = value;
                return true;
            }
            return false;
        }

        private static List<Layout> GetLayouts(Database database, Transaction transaction, bool includeModelSpace)
        {
            var layouts = new List<Layout>();
            var dictionary = (DBDictionary)transaction.GetObject(database.LayoutDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry entry in dictionary)
            {
                var layout = (Layout)transaction.GetObject(entry.Value, OpenMode.ForRead);
                if (!layout.ModelType || includeModelSpace)
                    layouts.Add(layout);
            }
            return layouts.OrderBy(layout => layout.TabOrder).ToList();
        }

        private static List<BlockReference> GetLayoutBlocks(
            Layout layout, string blockName, string tag, Transaction transaction)
        {
            var blocks = new List<BlockReference>();
            var layoutSpace = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
            foreach (ObjectId entityId in layoutSpace)
            {
                var block = transaction.GetObject(entityId, OpenMode.ForRead) as BlockReference;
                if (block != null &&
                    string.Equals(GetEffectiveBlockName(block, transaction), blockName, StringComparison.OrdinalIgnoreCase) &&
                    HasAttribute(block, tag, transaction))
                {
                    blocks.Add(block);
                }
            }
            return SortInReadingOrder(blocks);
        }

        private static List<BlockReference> SortInReadingOrder(IEnumerable<BlockReference> blocks)
        {
            var rows = new List<List<BlockReference>>();
            foreach (var block in blocks.OrderByDescending(item => item.Position.Y))
            {
                var row = rows.FirstOrDefault(items => Math.Abs(items[0].Position.Y - block.Position.Y) <= RowTolerance);
                if (row == null)
                {
                    row = new List<BlockReference>();
                    rows.Add(row);
                }
                row.Add(block);
            }

            return rows
                .OrderByDescending(row => row.Average(item => item.Position.Y))
                .SelectMany(row => row.OrderBy(item => item.Position.X))
                .ToList();
        }

        private static List<BlockReference> GetSelectedBlocks(
            ObjectId[] ids, string blockName, string tag, Transaction transaction)
        {
            var blocks = new List<BlockReference>();
            foreach (ObjectId id in ids)
            {
                var block = transaction.GetObject(id, OpenMode.ForRead) as BlockReference;
                if (block != null &&
                    string.Equals(GetEffectiveBlockName(block, transaction), blockName, StringComparison.OrdinalIgnoreCase) &&
                    HasAttribute(block, tag, transaction))
                {
                    blocks.Add(block);
                }
            }
            return SortInReadingOrder(blocks);
        }

        private static void NumberBlocks(
            Document document, NumberingSettingsViewModel settings, string blockName, ObjectId[] selectedIds)
        {
            int number = settings.StartNumber;
            int count = 0;

            using (var transaction = document.TransactionManager.StartTransaction())
            {
                IEnumerable<BlockReference> blocks;
                if (settings.Scope == NumberingScope.AllLayouts)
                {
                    blocks = GetLayouts(document.Database, transaction, settings.IncludeModelSpace)
                        .SelectMany(layout => GetLayoutBlocks(layout, blockName, settings.TagName, transaction));
                }
                else
                {
                    blocks = GetSelectedBlocks(selectedIds, blockName, settings.TagName, transaction);
                }

                foreach (var block in blocks)
                {
                    string value = settings.Prefix + number + settings.Suffix;
                    if (TrySetAttribute(block, settings.TagName, value, transaction))
                    {
                        number += settings.Increment;
                        count++;
                    }
                }
                transaction.Commit();
            }

            document.Editor.WriteMessage("\nПронумеровано блоков: " + count + ".");
        }
    }
}
