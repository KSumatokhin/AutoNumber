using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AutoNumber;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;

static class DrawingScheduleTests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Near(double expected, double actual) { Check(Math.Abs(expected-actual)<1e-8, "Expected " + expected + ", got " + actual); }
    static Document Example(bool model, double scale, double unitFactor)
    {
        var doc = new Document(); var db = doc.Database;
        db.CurrentSpaceId = model ? db.ModelId : db.PaperId; db.Dimscale = scale;
        AddStamp(doc, db.PaperId, new Point3d(178.99,401.93,0), 1, unitFactor, "2", "План сети электроснабжения 0.4кВ");
        doc.Editor.Point = new PromptPointResult { Status=PromptStatus.OK, Value=new Point3d(20,30,0) };
        return doc;
    }
    static void AddStamp(Document doc, ObjectId owner, Point3d position, double scale, double units, string number, string title)
    {
        var db=doc.Database; var space=(BlockTableRecord)db.Objects[owner];
        var stamp=new BlockReference { Position=position, ScaleFactors=new Scale3d(scale), UnitFactor=units };
        stamp.AttributeCollection.Add(db.Put(new AttributeReference { Tag="NUM", TextString=number }));
        space.AppendEntity(stamp);
        var p=position+new Vector3d(-100*scale,10*scale,0);
        space.AppendEntity(new DBText { TextString=title, Position=p, Box=new Extents3d(p,p+new Vector3d(30*scale,2*scale,0)) });
    }
    static List<MText> Written(Document d,ObjectId id) { return ((BlockTableRecord)d.Database.Objects[id]).Ids.Select(x=>d.Database.Objects[x]).OfType<MText>().ToList(); }
    static void Run(string name,Action test) { try {test(); Console.WriteLine("PASS: "+name);} catch(Exception ex) {throw new Exception(name+": "+ex.Message,ex);} }
    public static void Main()
    {
        Run("Captured stamp: DXF scale=1, UnitFactor=0.001",()=>{
            var d=Example(false,.5,.001); DrawingScheduleService.Run(d);
            var texts=Written(d,d.Database.PaperId).Concat(Written(d,d.Database.ModelId)).ToList();
            Check(texts.Count==2,"Two texts expected"); Check(texts[1].Contents=="План сети электроснабжения 0.4кВ","Title window was rescaled twice");
        });
        Run("Paper: current space, height 2.5, proportional spacing",()=>{
            var d=Example(false,.5,.001); AddStamp(d,d.Database.PaperId,new Point3d(500,400,0),1,1,"3","Второй лист"); DrawingScheduleService.Run(d);
            var texts=Written(d,d.Database.PaperId); Check(texts.Count==4,"Must write on paper"); Check(Written(d,d.Database.ModelId).Count==0,"Model changed");
            foreach(var t in texts)Near(2.5,t.TextHeight);
            Near(20,texts[0].Location.X);Near(30,texts[0].Location.Y);Near(9.5,texts[1].Location.X-texts[0].Location.X);Near(8,texts[0].Location.Y-texts[2].Location.Y);
            Near(.5,d.Database.Dimscale);
        });
        Run("Model including model editing through viewport: direct DIMSCALE",()=>{
            foreach(var scale in new[]{.5,1,100}) {
                var d=Example(true,scale,.001); DrawingScheduleService.Run(d); var texts=Written(d,d.Database.ModelId);
                Check(texts.Count==2,"Must write in active model space"); Near(2.5*scale,texts[0].TextHeight);Near(9.5*scale,texts[1].Location.X-texts[0].Location.X);
                Check(Written(d,d.Database.PaperId).Count==0,"Paper changed");Near(scale,d.Database.Dimscale);
            }
        });
        Run("Unit metadata does not rescale block geometry",()=>{
            foreach(var factor in new[]{.001,1,1000}) foreach(var scale in new[]{.5,2.0}) {
                var d=new Document();d.Database.CurrentSpaceId=d.Database.PaperId;
                AddStamp(d,d.Database.PaperId,new Point3d(200,200,0),scale,factor,"1","Верное название");DrawingScheduleService.Run(d);
                Check(Written(d,d.Database.PaperId)[1].Contents=="Верное название","Incorrect stamp scale");
            }
        });
        Run("Insertion uses shifted and rotated UCS",()=>{
            var d=Example(false,100,1);d.Editor.CurrentUserCoordinateSystem=new Matrix3d(Math.PI/2,100,200);DrawingScheduleService.Run(d);
            var texts=Written(d,d.Database.PaperId);Near(70,texts[0].Location.X);Near(220,texts[0].Location.Y);Near(Math.PI/2,texts[0].Rotation);
            Near(70,texts[1].Location.X);Near(229.5,texts[1].Location.Y);
        });
        Run("No title from another layout; duplicates retained",()=>{
            var d=Example(false,100,1);var space=(BlockTableRecord)d.Database.Objects[d.Database.PaperId];
            var title=space.Ids.First(id=>d.Database.Objects[id] is DBText);space.Ids.Remove(title);
            ((BlockTableRecord)d.Database.Objects[d.Database.ModelId]).Ids.Add(title);
            AddStamp(d,d.Database.PaperId,new Point3d(500,400,0),1,1,"2","Дубликат номера");DrawingScheduleService.Run(d);
            var texts=Written(d,d.Database.PaperId);Check(texts.Count==4,"Duplicates must be retained");Check(texts[1].Contents.Contains("ЛИСТ БЕЗ НАЗВАНИЯ"),"Cross-layout title leak");
        });
        Run("Esc and invalid model DIMSCALE write nothing",()=>{
            var d=Example(false,0,1);d.Editor.Point.Status=PromptStatus.Cancel;DrawingScheduleService.Run(d);Check(Written(d,d.Database.ModelId).Count+Written(d,d.Database.PaperId).Count==0,"Cancel wrote texts");
            foreach(var scale in new[]{0,-1,double.NaN,double.PositiveInfinity}) {
                d=Example(true,scale,1);bool failed=false;try{DrawingScheduleService.Run(d);}catch(InvalidOperationException){failed=true;}
                Check(failed,"Invalid DIMSCALE accepted");Check(Written(d,d.Database.ModelId).Count==0,"Invalid scale wrote texts");
            }
            d=Example(false,0,1);DrawingScheduleService.Run(d);Near(2.5,Written(d,d.Database.PaperId)[0].TextHeight);
        });
        Console.WriteLine("DrawingScheduleTests: "+checks+" checks passed (actual service, CAD adapters replaced).");
    }
}
namespace AutoNumber
{
    static class AutoNumberCommand
    {
        internal static List<Layout> GetLayouts(Database d,Transaction t,bool includeModel) {return d.Layouts;}
        internal static string GetEffectiveBlockName(BlockReference b,Transaction t) {return "Штамп";}
    }
}
namespace Bricscad.ApplicationServices
{
    public class Document {public Database Database=new Database();public Editor Editor=new Editor();public TransactionManager TransactionManager {get{return Database.TransactionManager;}}}
}
namespace Bricscad.EditorInput
{
    public enum PromptStatus {OK,Cancel}
    public class PromptPointResult {public PromptStatus Status=PromptStatus.OK;public Point3d Value;}
    public class Editor {public PromptPointResult Point=new PromptPointResult();public Matrix3d CurrentUserCoordinateSystem=new Matrix3d(0,0,0);public PromptPointResult GetPoint(string p){return Point;} public void WriteMessage(string m){}}
}
namespace Teigha.Geometry
{
    public struct Point3d {public double X,Y,Z;public Point3d(double x,double y,double z){X=x;Y=y;Z=z;}public static Point3d operator+(Point3d p,Vector3d v){return new Point3d(p.X+v.X,p.Y+v.Y,p.Z+v.Z);}public Point3d TransformBy(Matrix3d m){return new Point3d(Math.Cos(m.Angle)*X-Math.Sin(m.Angle)*Y+m.X,Math.Sin(m.Angle)*X+Math.Cos(m.Angle)*Y+m.Y,Z);}}
    public struct Vector3d {public double X,Y,Z;public static Vector3d ZAxis=new Vector3d(0,0,1);public Vector3d(double x,double y,double z){X=x;Y=y;Z=z;}public Vector3d RotateBy(double a,Vector3d axis){return new Vector3d(Math.Cos(a)*X-Math.Sin(a)*Y,Math.Sin(a)*X+Math.Cos(a)*Y,Z);}}
    public struct Scale3d {public double X;public Scale3d(double v){X=v;}}
    public struct Extents3d {public Point3d MinPoint,MaxPoint;public Extents3d(Point3d a,Point3d b){MinPoint=a;MaxPoint=b;}}
    public struct Matrix3d {public double Angle,X,Y;public Matrix3d(double a,double x,double y){Angle=a;X=x;Y=y;}}
}
namespace Teigha.DatabaseServices
{
    public struct ObjectId {public int Value;public static implicit operator ObjectId(int v){return new ObjectId{Value=v};}public static bool operator==(ObjectId a,ObjectId b){return a.Value==b.Value;}public static bool operator!=(ObjectId a,ObjectId b){return !(a==b);}public override bool Equals(object o){return o is ObjectId&&(ObjectId)o==this;}public override int GetHashCode(){return Value;}}
    public enum OpenMode {ForRead,ForWrite}
    public enum AttachmentPoint {BottomLeft,BottomCenter}
    public enum LineSpacingStyle {Exactly}
    public class DBObject { }
    public class Entity:DBObject {public Extents3d Box;public virtual Extents3d GeometricExtents {get{return Box;}}}
    public class DBText:Entity {public string TextString;public Point3d Position;}
    public class AttributeReference:DBText {public string Tag;}
    public class BlockReference:Entity {public Point3d Position;public Scale3d ScaleFactors;public double UnitFactor,Rotation;public List<ObjectId> AttributeCollection=new List<ObjectId>();}
    public class MText:Entity {public Point3d Location;public string Contents;public double TextHeight,Rotation,LineSpacingFactor;public short ColorIndex;public AttachmentPoint Attachment;public LineSpacingStyle LineSpacingStyle;public ObjectId LayerId,TextStyleId;public void TransformBy(Matrix3d m){Location=Location.TransformBy(m);Rotation+=m.Angle;}}
    public class Layout:DBObject {public ObjectId BlockTableRecordId;}
    public class BlockTable:DBObject {public const string ModelSpace="*Model_Space";public ObjectId ModelId;public ObjectId this[string name]{get{return ModelId;}}}
    public class LayerTable:DBObject {public bool Has(string s){return false;}public ObjectId this[string s]{get{return 0;}}}
    public class TextStyleTable:DBObject {public bool Has(string s){return false;}public ObjectId this[string s]{get{return 0;}}}
    public class BlockTableRecord:DBObject,IEnumerable {public const string ModelSpace="*Model_Space";public List<ObjectId> Ids=new List<ObjectId>();readonly Database db;public BlockTableRecord(Database d){db=d;}public void AppendEntity(Entity e){Ids.Add(db.Put(e));}public IEnumerator GetEnumerator(){return Ids.GetEnumerator();}}
    public class Database
    {
        public Dictionary<ObjectId,DBObject> Objects=new Dictionary<ObjectId,DBObject>();public List<Layout> Layouts=new List<Layout>();public ObjectId BlockTableId,LayerTableId,TextStyleTableId,CurrentSpaceId,ModelId,PaperId;public double Dimscale=100;public TransactionManager TransactionManager;
        public Database(){TransactionManager=new TransactionManager(this);ModelId=Put(new BlockTableRecord(this));PaperId=Put(new BlockTableRecord(this));BlockTableId=Put(new BlockTable{ModelId=ModelId});LayerTableId=Put(new LayerTable());TextStyleTableId=Put(new TextStyleTable());CurrentSpaceId=ModelId;Layouts.Add(new Layout{BlockTableRecordId=ModelId});Layouts.Add(new Layout{BlockTableRecordId=PaperId});}
        public ObjectId Put(DBObject value){ObjectId id=Objects.Count+1;Objects.Add(id,value);return id;}
    }
    public class TransactionManager {readonly Database db;public TransactionManager(Database d){db=d;}public Transaction StartTransaction(){return new Transaction(db);}}
    public class Transaction:IDisposable {readonly Database db;public Transaction(Database d){db=d;}public DBObject GetObject(ObjectId id,OpenMode mode){return db.Objects[id];}public void Commit(){}public void AddNewlyCreatedDBObject(DBObject o,bool add){}public void Dispose(){}}
}
