using AutoNumber.Services;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace AutoNumber
{
    internal static class DrawingScheduleService
    {
        private const string StampBlockName = "Штамп";
        private const string NumberTag = "NUM";
        private const string MissingTitle = "-== !!! ЛИСТ БЕЗ НАЗВАНИЯ !!! ==-";
        private const double RowStep = 800.0;

        private sealed class ScheduleRow
        {
            public double Number { get; set; }
            public string NumberText { get; set; }
            public string Title { get; set; }
        }

        private sealed class TextItem
        {
            public string Text { get; set; }
            public Point3d Position { get; set; }
            public Extents3d Bounds { get; set; }
        }

        public static void Run(Document document)
        {
            List<ScheduleRow> rows;
            using (var transaction = document.TransactionManager.StartTransaction())
            {
                rows = CollectRows(document.Database, transaction);
                transaction.Commit();
            }

            if (rows.Count == 0)
            {
                document.Editor.WriteMessage("\nБлоки Штамп с атрибутом NUM не найдены.");
                return;
            }

            var pointResult = document.Editor.GetPoint("\nУкажите точку вставки ведомости: ");
            if (pointResult.Status != PromptStatus.OK)
                return;

            WriteRows(document.Database, rows, pointResult.Value);
            document.Editor.WriteMessage("\nСтрок ведомости создано: " + rows.Count + ".");
        }

        private static List<ScheduleRow> CollectRows(Database database, Transaction transaction)
        {
            var rows = new List<ScheduleRow>();
            foreach (Layout layout in AutoNumberCommand.GetLayouts(database, transaction, true))
            {
                var space = (BlockTableRecord)transaction.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                var entities = space.Cast<ObjectId>()
                    .Select(id => transaction.GetObject(id, OpenMode.ForRead))
                    .ToList();

                foreach (BlockReference block in entities.OfType<BlockReference>())
                {
                    if (!string.Equals(AutoNumberCommand.GetEffectiveBlockName(block, transaction),
                            StampBlockName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string numberText = GetAttribute(block, NumberTag, transaction);
                    double number;
                    if (!TryParseNumber(numberText, out number) || number <= 0.0)
                        continue;

                    Extents3d window = GetTitleWindow(block);
                    List<TextItem> texts = entities
                        .Select(GetTextItem)
                        .Where(item => item != null && IsCrossing(item.Bounds, window))
                        .OrderByDescending(item => item.Position.Y)
                        .ThenBy(item => item.Position.X)
                        .ToList();

                    string title = string.Join(" ", texts.Select(item => item.Text).Where(text => text.Length > 0));
                    rows.Add(new ScheduleRow
                    {
                        Number = number,
                        NumberText = number.ToString("0.##", CultureInfo.InvariantCulture),
                        Title = string.IsNullOrWhiteSpace(title) ? MissingTitle : title
                    });
                }
            }
            return rows.OrderBy(row => row.Number).ToList();
        }

        private static string GetAttribute(BlockReference block, string tag, Transaction transaction)
        {
            foreach (ObjectId id in block.AttributeCollection)
            {
                var attribute = transaction.GetObject(id, OpenMode.ForRead) as AttributeReference;
                if (attribute != null && string.Equals(attribute.Tag, tag, StringComparison.OrdinalIgnoreCase))
                    return attribute.TextString;
            }
            return null;
        }

        private static bool TryParseNumber(string value, out double number)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                   double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
        }

        private static Extents3d GetTitleWindow(BlockReference block)
        {
            double scale = Math.Abs(block.UnitFactor * block.ScaleFactors.X);
            Vector3d first = new Vector3d(-120.0 * scale, 15.0 * scale, 0.0)
                .RotateBy(block.Rotation, Vector3d.ZAxis);
            Vector3d second = new Vector3d(-50.0 * scale, 0.0, 0.0)
                .RotateBy(block.Rotation, Vector3d.ZAxis);
            Point3d p1 = block.Position + first;
            Point3d p2 = block.Position + second;
            return new Extents3d(
                new Point3d(Math.Min(p1.X, p2.X), Math.Min(p1.Y, p2.Y), 0.0),
                new Point3d(Math.Max(p1.X, p2.X), Math.Max(p1.Y, p2.Y), 0.0));
        }

        private static TextItem GetTextItem(DBObject value)
        {
            var mtext = value as MText;
            if (mtext != null)
                return CreateTextItem(MTextFormatService.RemoveFormatting(mtext.Contents), mtext.Location, mtext);

            var text = value as DBText;
            if (text != null)
                return CreateTextItem(text.TextString.Trim(), text.Position, text);

            return null;
        }

        private static TextItem CreateTextItem(string text, Point3d position, Entity entity)
        {
            try
            {
                return new TextItem { Text = text, Position = position, Bounds = entity.GeometricExtents };
            }
            catch
            {
                return new TextItem { Text = text, Position = position, Bounds = new Extents3d(position, position) };
            }
        }

        private static bool IsCrossing(Extents3d bounds, Extents3d window)
        {
            return bounds.MaxPoint.X >= window.MinPoint.X && bounds.MinPoint.X <= window.MaxPoint.X &&
                   bounds.MaxPoint.Y >= window.MinPoint.Y && bounds.MinPoint.Y <= window.MaxPoint.Y;
        }

        private static void WriteRows(Database database, IList<ScheduleRow> rows, Point3d insertionPoint)
        {
            using (var transaction = database.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)transaction.GetObject(table[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var layers = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
                var styles = (TextStyleTable)transaction.GetObject(database.TextStyleTableId, OpenMode.ForRead);

                for (int index = 0; index < rows.Count; index++)
                {
                    double y = insertionPoint.Y - index * RowStep;
                    AddMText(model, transaction, rows[index].NumberText,
                        new Point3d(insertionPoint.X, y, insertionPoint.Z), AttachmentPoint.BottomCenter,
                        layers, styles);
                    AddMText(model, transaction, rows[index].Title,
                        new Point3d(insertionPoint.X + 950.0, y, insertionPoint.Z), AttachmentPoint.BottomLeft,
                        layers, styles);
                }
                transaction.Commit();
            }
        }

        private static void AddMText(BlockTableRecord model, Transaction transaction, string contents,
            Point3d location, AttachmentPoint attachment, LayerTable layers, TextStyleTable styles)
        {
            var text = new MText
            {
                Location = location,
                Contents = contents,
                TextHeight = 250.0,
                Attachment = attachment,
                Rotation = 0.0,
                LineSpacingFactor = 0.96,
                ColorIndex = 256
            };
            if (layers.Has("EL_TEXT"))
                text.LayerId = layers["EL_TEXT"];
            if (styles.Has("GOST 2.304"))
                text.TextStyleId = styles["GOST 2.304"];

            model.AppendEntity(text);
            transaction.AddNewlyCreatedDBObject(text, true);
        }
    }
}
