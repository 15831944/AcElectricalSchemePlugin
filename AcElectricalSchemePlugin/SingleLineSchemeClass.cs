﻿using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.EditorInput;
using System.Data;
using System.Data.OleDb;

namespace AcElectricalSchemePlugin
{
    static class SingleLineSchemeClass
    {
        private static Document acDoc;
        private static Editor editor;
        private static Point3d currentSheetPoint;
        private static Point3d currentPoint;
        private static Point3d prevAutomatic;
        private static int sinFilter = 1;
        private static int currentSection = 1;
        private static DynamicBlockReferenceProperty curSheet;
        private static int currentSheetNumber = 1;
        private static int curSheetsNumber = 2;
        private static int maxSheets = 0;
        private static bool aborted = false;

        private struct Unit
        {
            public int automatic; //33

            public string phases; //2
            public string planName; //3
            public string nominalPower; //4
            public string calcPower; //5
            public string calcElectriciry; //6
            public string consumerName; //7
            public string contructName; //8
            public string cosPhi; //9
            public string calcElecFold; //10
            public string allowableDeviations; //11
            public string cableCrossSection; //12

            public string switchName; //13
            public string switchType; //14
            public string switchNominalElec; //15
            public string switchNominalVoltage; //16

            public string protectName; //17
            public string protectType; //18
            public string protectNominalElec; //19
            public string protectNominalVoltage; //20
            public string disconnector; //21
            public string protectTermDefense; //22
            public string shortCircuitDefense; //23
            public string protectDefenseReactTime; //24
            public string ultBreakCapacity;//25
           
            public string switchboardName; //26
            public string switchboardType; //27
            public string switchboardNominalElec; //28
            public string switchboardNominalVoltage; //29
            public string switchboardTermDefense; //30
            public string switchboardDefenseReactTime; //31
        }

        public static void drawScheme(string fileName)
        {
            acDoc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            Database acDb = acDoc.Database;

            List<Unit> units = loadData(fileName);

            PromptPointResult startPoint = editor.GetPoint("\nВыберите точку старта");
            if (startPoint.Status != PromptStatus.OK)
            {
                editor.WriteMessage("Неверный ввод...");
                return;
            }
            else currentSheetPoint = startPoint.Value;

            while (maxSheets < 2 || maxSheets > 20)
            {
                PromptIntegerResult sheetsNum = editor.GetInteger("\nВведите максимальное количество листов А4 в одной рамке (от 2 до 20): ");
                if (sheetsNum.Status != PromptStatus.OK)
                {
                    editor.WriteMessage("Неверный ввод...");
                    return;
                }
                else maxSheets = sheetsNum.Value;
            }

            using (DocumentLock docLock = acDoc.LockDocument())
            {
                using (Transaction acTrans = acDb.TransactionManager.StartTransaction())
                {
                    BlockTable acBlkTbl;
                    acBlkTbl = acTrans.GetObject(acDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord acModSpace;
                    acModSpace = acTrans.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    prevAutomatic = new Point3d();
                    sinFilter = 1;
                    currentSection = 1;

                    insertSheet(acTrans, acModSpace, acDb, currentSheetPoint);
                    prevAutomatic = currentSheetPoint.Add(new Vector3d(85, 0, 0));
                    currentPoint = currentSheetPoint.Add(new Vector3d(110, -65, 0));
                    for (int i = 0; i < units.Count; i++)
                    {
                        insertUnit(acModSpace, acDb, units[i]);
                        if (aborted)
                        {
                            aborted = false;
                            currentSheetNumber++;
                            currentSheetPoint = currentSheetPoint.Add(new Vector3d(0, -327, 0));
                            insertSheet(acTrans, acModSpace, acDb, currentSheetPoint);
                            prevAutomatic = currentSheetPoint.Add(new Vector3d(85, 0, 0));
                            currentPoint = currentSheetPoint.Add(new Vector3d(110, -65, 0));
                            curSheetsNumber = 2;
                            i--;
                        }
                    }
                    acTrans.Commit();
                }
            }
            sinFilter = 1;
            currentSection = 1;
            currentSheetNumber = 1;
            curSheetsNumber = 2;
            maxSheets = 0;
            aborted = false;
        }

        private static List<Unit> loadData(string path)
        {
            List<Unit> units = new List<Unit>();
            DataSet dataSet = new DataSet("EXCEL");
            string connectionString;
            connectionString = "Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + path + ";Extended Properties='Excel 12.0;HDR=NO;'";
            OleDbConnection connection = new OleDbConnection(connectionString);
            connection.Open();

            System.Data.DataTable schemaTable = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, new object[] { null, null, null, "TABLE" });
            string sheet1 = (string)schemaTable.Rows[0].ItemArray[2];

            string select = String.Format("SELECT * FROM [{0}]", sheet1);
            OleDbDataAdapter adapter = new OleDbDataAdapter(select, connection);
            adapter.FillSchema(dataSet, SchemaType.Mapped);
            adapter.Fill(dataSet);
            connection.Close();

            for (int column = 3; column < dataSet.Tables[0].Columns.Count; column++)
            {
                Unit unit = new Unit();
                unit.phases = dataSet.Tables[0].Rows[1][column].ToString();
                unit.planName = dataSet.Tables[0].Rows[2][column].ToString();
                unit.nominalPower = dataSet.Tables[0].Rows[3][column].ToString();
                unit.calcPower = dataSet.Tables[0].Rows[4][column].ToString();
                unit.calcElectriciry = dataSet.Tables[0].Rows[5][column].ToString();
                unit.consumerName = dataSet.Tables[0].Rows[6][column].ToString();
                unit.contructName = dataSet.Tables[0].Rows[7][column].ToString();
                unit.cosPhi = dataSet.Tables[0].Rows[8][column].ToString();
                unit.calcElecFold = dataSet.Tables[0].Rows[9][column].ToString();
                unit.allowableDeviations = dataSet.Tables[0].Rows[10][column].ToString();
                unit.cableCrossSection = dataSet.Tables[0].Rows[11][column].ToString();
                unit.switchName = dataSet.Tables[0].Rows[12][column].ToString();
                unit.switchType = dataSet.Tables[0].Rows[13][column].ToString();
                unit.switchNominalElec = dataSet.Tables[0].Rows[14][column].ToString();
                unit.switchNominalVoltage = dataSet.Tables[0].Rows[15][column].ToString();
                unit.protectName = dataSet.Tables[0].Rows[16][column].ToString();
                unit.protectType = dataSet.Tables[0].Rows[17][column].ToString();
                unit.protectNominalElec = dataSet.Tables[0].Rows[18][column].ToString();
                unit.protectNominalVoltage = dataSet.Tables[0].Rows[19][column].ToString();
                unit.disconnector = dataSet.Tables[0].Rows[20][column].ToString();
                unit.protectTermDefense = dataSet.Tables[0].Rows[21][column].ToString();
                unit.shortCircuitDefense = dataSet.Tables[0].Rows[22][column].ToString();
                unit.protectDefenseReactTime = dataSet.Tables[0].Rows[23][column].ToString();
                unit.ultBreakCapacity = dataSet.Tables[0].Rows[24][column].ToString();
                unit.switchboardName = dataSet.Tables[0].Rows[25][column].ToString();
                unit.switchboardType = dataSet.Tables[0].Rows[26][column].ToString();
                unit.switchboardNominalElec = dataSet.Tables[0].Rows[27][column].ToString();
                unit.switchboardNominalVoltage = dataSet.Tables[0].Rows[28][column].ToString();
                unit.switchboardTermDefense = dataSet.Tables[0].Rows[29][column].ToString();
                unit.switchboardDefenseReactTime = dataSet.Tables[0].Rows[30][column].ToString();
                int.TryParse(dataSet.Tables[0].Rows[32][column].ToString(), out unit.automatic);
                if (unit.automatic == 0) break;
                units.Add(unit);
            }
            return units;
        }

        private static void insertUnit(BlockTableRecord modSpace, Database acdb, Unit unit)
        {
            using (Transaction acTrans = acdb.TransactionManager.StartTransaction())
            {
                #region automatic
                Point3d minPoint = new Point3d(0, 0, 0);
                Point3d maxPoint = new Point3d(0, 0, 0);
                Point3d lowestPoint = new Point3d(0, 0, 0);
                ObjectIdCollection ids = new ObjectIdCollection();
                string filename = @"Data\Automatic.dwg";
                string blockName = "Automatic";
                using (Database sourceDb = new Database(false, true))
                {
                    if (System.IO.File.Exists(filename))
                    {
                        sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                        using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                        {
                            BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                            if (bt.Has(blockName))
                                ids.Add(bt[blockName]);
                            trans.Commit();
                        }
                    }
                    else editor.WriteMessage("Не найден файл {0}", filename);
                    if (ids.Count > 0)
                    {
                        acTrans.TransactionManager.QueueForGraphicsFlush();
                        IdMapping iMap = new IdMapping();
                        acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                        BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                        if (bt.Has(blockName))
                        {
                            BlockReference br = new BlockReference(currentPoint, bt[blockName]);
                            br.Layer = "0";
                            modSpace.AppendEntity(br);
                            acTrans.AddNewlyCreatedDBObject(br, true);
                            DynamicBlockReferenceProperty property = null;
                            object value = null;
                            if (br.IsDynamicBlock)
                            {
                                DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
                                foreach (DynamicBlockReferenceProperty prop in props)
                                {
                                    object[] values = prop.GetAllowedValues();
                                    if (prop.PropertyName == "Видимость1" && !prop.ReadOnly)
                                    {
                                        prop.Value = values[unit.automatic-1];
                                        value = values[unit.automatic-1];
                                        property = prop;
                                        break;
                                    }
                                }
                            }
                            BlockTableRecord btr = bt[blockName].GetObject(OpenMode.ForWrite) as BlockTableRecord;
                            foreach (ObjectId id in btr)
                            {
                                DBObject obj = id.GetObject(OpenMode.ForWrite);
                                AttributeDefinition attDef = obj as AttributeDefinition;
                                if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                                {
                                    #region attributes
                                    switch (attDef.Tag)
                                    {
                                        case "?ГР1KA":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardName;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        //case "?ГР1УЗО":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.switchboardName;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        case "?ГР2KM":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardName;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        //case "?ГРКК1":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.switchboardName;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        case "?ГРКМ1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardName;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "№ГР1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.protectName;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "№ГР1КМ":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardName;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "НОМИНАЛ":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.protectNominalElec;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "НОМИНАЛ_KM2":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardNominalElec;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        //case "НОМИНАЛ_КК1":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.switchboardNominalElec;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        case "НОМИНАЛ_КМ":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardNominalElec;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "НОМИНАЛ_КМ1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.switchboardNominalElec;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        //case "НОМИНАЛ_УЗО":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.protectNominalElec;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        //case "НОМИНАЛ_УЗО1":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.protectNominalElec;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        //case "ТОК_УТЕЧКИ":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.ultBreakCapacity;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        //case "ТОК_УТЕЧКИ1":
                                        //    {
                                        //        using (AttributeReference attRef = new AttributeReference())
                                        //        {
                                        //            attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                        //            attRef.TextString = unit.ultBreakCapacity;
                                        //            br.AttributeCollection.AppendAttribute(attRef);
                                        //            acTrans.AddNewlyCreatedDBObject(attRef, true);
                                        //        }
                                        //        break;
                                        //    }
                                        case "УСТАВКА":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.protectTermDefense;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "УСТАВКА1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.protectTermDefense;
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "ФАЗА":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.phases.Contains("/") ? unit.phases.Split('/')[1] : "-";
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "ФАЗА1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.phases.Contains("/") ? unit.phases.Split('/')[1] : "-";
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "ФАЗА2":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.phases.Contains("/") ? unit.phases.Split('/')[1] : "-";
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                        case "ХАРАКТЕРИСТИКА1":
                                            {
                                                using (AttributeReference attRef = new AttributeReference())
                                                {
                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                    attRef.TextString = unit.protectDefenseReactTime.Replace("Характеристика ", "");
                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                }
                                                break;
                                            }
                                    }
                                    #endregion
                                }
                            }
                            getSize(br, ref minPoint, ref maxPoint);
                            while (minPoint.X < prevAutomatic.X)
                            {
                                br.Position = br.Position.Add(new Vector3d(1, 0, 0));
                                minPoint = minPoint.Add(new Vector3d(1, 0, 0));
                                maxPoint = maxPoint.Add(new Vector3d(1, 0, 0));
                            }
                            if (br.IsDynamicBlock)
                            {
                                br.ResetBlock();
                                property.Value = value;
                            }
                            if (maxPoint.X >= currentSheetPoint.X + 210 * (curSheetsNumber-1))
                            {
                                if (curSheetsNumber<maxSheets)
                                {
                                    curSheetsNumber++;
                                    curSheet.Value = curSheet.GetAllowedValues()[curSheetsNumber - 1];
                                    editor.Regen();
                                }
                                else
                                {
                                    acTrans.Abort();
                                    aborted = true;
                                    return;
                                }
                            }
                            prevAutomatic = maxPoint;
                            lowestPoint = getLowestPoint(br);
                        }
                    }
                    else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                }
                #endregion

                if (!(unit.consumerName == "АВР" || unit.consumerName == "авр" || unit.consumerName == "avr" || unit.consumerName == "AVR"))
                {
                    if (!unit.consumerName.Contains("Резерв"))
                    {
                        
                        if (unit.switchboardName.Contains("VFD"))
                        {
                            #region VFD
                            ids = new ObjectIdCollection();
                            filename = @"Data\FreqConv.dwg";
                            blockName = "FreqConv";
                            using (Database sourceDb = new Database(false, true))
                            {
                                if (System.IO.File.Exists(filename))
                                {
                                    sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                                    using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                                    {
                                        BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                                        if (bt.Has(blockName))
                                            ids.Add(bt[blockName]);
                                        trans.Commit();
                                    }
                                }
                                else editor.WriteMessage("Не найден файл {0}", filename);
                                if (ids.Count > 0)
                                {
                                    acTrans.TransactionManager.QueueForGraphicsFlush();
                                    IdMapping iMap = new IdMapping();
                                    acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                                    BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                                    if (bt.Has(blockName))
                                    {
                                        BlockReference br = new BlockReference(lowestPoint, bt[blockName]);
                                        br.Layer = "0";
                                        modSpace.AppendEntity(br);
                                        acTrans.AddNewlyCreatedDBObject(br, true);
                                        Point3d min = new Point3d(0, 0, 0);
                                        Point3d max = new Point3d(0, 0, 0);
                                        getSize(br, ref min, ref max);
                                        if (max.X > prevAutomatic.X)
                                            prevAutomatic = max;
                                        BlockTableRecord btr = bt[blockName].GetObject(OpenMode.ForWrite) as BlockTableRecord;
                                        foreach (ObjectId id in btr)
                                        {
                                            DBObject obj = id.GetObject(OpenMode.ForWrite);
                                            AttributeDefinition attDef = obj as AttributeDefinition;
                                            if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                                            {
                                                #region attributes
                                                switch (attDef.Tag)
                                                {
                                                    case "NUZ":
                                                        {
                                                            using (AttributeReference attRef = new AttributeReference())
                                                            {
                                                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                attRef.TextString = unit.switchboardName;
                                                                br.AttributeCollection.AppendAttribute(attRef);
                                                                acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                            }
                                                            break;
                                                        }
                                                    case "P_UZ":
                                                        {
                                                            using (AttributeReference attRef = new AttributeReference())
                                                            {
                                                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                attRef.TextString = unit.nominalPower;
                                                                br.AttributeCollection.AppendAttribute(attRef);
                                                                acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                            }
                                                            break;
                                                        }
                                                }
                                                #endregion
                                            }
                                        }
                                        lowestPoint = getLowestPoint(br);
                                    }
                                }
                                else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                            }
                        #endregion
                            #region sinFilter
                            int nominalElectricity = 0;
                            string str = "";
                            if (unit.switchboardNominalElec.Contains(" "))
                                str = unit.switchboardNominalElec.Split(' ')[0];
                            else str = unit.switchboardNominalElec;
                            int.TryParse(str, out nominalElectricity);
                            if (nominalElectricity < 78 && unit.switchboardNominalElec != "-" && nominalElectricity != 0)
                            {
                                ids = new ObjectIdCollection();
                                filename = @"Data\SinFilter.dwg";
                                blockName = "SinFilter";
                                using (Database sourceDb = new Database(false, true))
                                {
                                    if (System.IO.File.Exists(filename))
                                    {
                                        sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                                        using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                                        {
                                            BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                                            if (bt.Has(blockName))
                                                ids.Add(bt[blockName]);
                                            trans.Commit();
                                        }
                                    }
                                    else editor.WriteMessage("Не найден файл {0}", filename);
                                    if (ids.Count > 0)
                                    {
                                        acTrans.TransactionManager.QueueForGraphicsFlush();
                                        IdMapping iMap = new IdMapping();
                                        acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                                        BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                                        if (bt.Has(blockName))
                                        {
                                            BlockReference br = new BlockReference(lowestPoint, bt[blockName]);
                                            br.Layer = "0";
                                            modSpace.AppendEntity(br);
                                            acTrans.AddNewlyCreatedDBObject(br, true);
                                            BlockTableRecord btr = bt[blockName].GetObject(OpenMode.ForWrite) as BlockTableRecord;
                                            foreach (ObjectId id in btr)
                                            {
                                                DBObject obj = id.GetObject(OpenMode.ForWrite);
                                                AttributeDefinition attDef = obj as AttributeDefinition;
                                                if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                                                {
                                                    #region attributes
                                                    switch (attDef.Tag)
                                                    {
                                                        case "NL":
                                                            {
                                                                using (AttributeReference attRef = new AttributeReference())
                                                                {
                                                                    string sfNAme = currentSection + "Z" + sinFilter;
                                                                    attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                    attRef.TextString = sfNAme;
                                                                    br.AttributeCollection.AppendAttribute(attRef);
                                                                    acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                                }
                                                                break;
                                                            }
                                                    }
                                                    #endregion
                                                }
                                            }
                                            lowestPoint = getLowestPoint(br);
                                        }
                                        sinFilter++;
                                    }
                                    else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                                }
                            }
                            #endregion
                        }

                        #region switchAutomatic
                        if (unit.switchName != "-")
                        {
                            ids = new ObjectIdCollection();
                            filename = @"Data\Automatic.dwg";
                            blockName = "Automatic";
                            using (Database sourceDb = new Database(false, true))
                            {
                                if (System.IO.File.Exists(filename))
                                {
                                    sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                                    using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                                    {
                                        BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                                        if (bt.Has(blockName))
                                            ids.Add(bt[blockName]);
                                        trans.Commit();
                                    }
                                }
                                else editor.WriteMessage("Не найден файл {0}", filename);
                                if (ids.Count > 0)
                                {
                                    acTrans.TransactionManager.QueueForGraphicsFlush();
                                    IdMapping iMap = new IdMapping();
                                    acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                                    BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                                    if (bt.Has(blockName))
                                    {
                                        BlockReference br = new BlockReference(lowestPoint.Add(new Vector3d(0, -41.4073, 0)), bt[blockName]);
                                        br.Layer = "0";
                                        modSpace.AppendEntity(br);
                                        acTrans.AddNewlyCreatedDBObject(br, true);
                                        DynamicBlockReferenceProperty property = null;
                                        object value = null;
                                        if (br.IsDynamicBlock)
                                        {
                                            DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
                                            foreach (DynamicBlockReferenceProperty prop in props)
                                            {
                                                object[] values = prop.GetAllowedValues();
                                                if (prop.PropertyName == "Видимость1" && !prop.ReadOnly)
                                                {
                                                    prop.Value = values[unit.automatic - 1 + 4];
                                                    value = values[unit.automatic - 1 + 4];
                                                    property = prop;
                                                    break;
                                                }
                                            }
                                        }
                                        BlockTableRecord btr = bt[blockName].GetObject(OpenMode.ForWrite) as BlockTableRecord;
                                        foreach (ObjectId id in btr)
                                        {
                                            DBObject obj = id.GetObject(OpenMode.ForWrite);
                                            AttributeDefinition attDef = obj as AttributeDefinition;
                                            if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                                            {
                                                #region attributes
                                                switch (attDef.Tag)
                                                {
                                                    case "№ГР1":
                                                        {
                                                            using (AttributeReference attRef = new AttributeReference())
                                                            {
                                                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                attRef.TextString = unit.switchName;
                                                                br.AttributeCollection.AppendAttribute(attRef);
                                                                acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                            }
                                                            break;
                                                        }
                                                    case "НОМИНАЛ":
                                                        {
                                                            using (AttributeReference attRef = new AttributeReference())
                                                            {
                                                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                attRef.TextString = unit.switchNominalElec;
                                                                br.AttributeCollection.AppendAttribute(attRef);
                                                                acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                            }
                                                            break;
                                                        }
                                                    case "ФАЗА1":
                                                        {
                                                            using (AttributeReference attRef = new AttributeReference())
                                                            {
                                                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                                attRef.TextString = unit.phases.Contains("/") ? unit.phases.Split('/')[1] : "-";
                                                                br.AttributeCollection.AppendAttribute(attRef);
                                                                acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                            }
                                                            break;
                                                        }
                                                }
                                                #endregion
                                            }
                                        }
                                        lowestPoint = getLowestPoint(br);
                                    }
                                }
                                else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                            }
                        }
                        #endregion

                        double endPointY = 125 - (currentPoint.Y - lowestPoint.Y);
                        Line cableLine = new Line();
                        cableLine.SetDatabaseDefaults();
                        cableLine.Layer = "ER-DWG";
                        cableLine.StartPoint = lowestPoint;
                        cableLine.EndPoint = cableLine.StartPoint.Add(new Vector3d(0, -endPointY, 0));
                        modSpace.AppendEntity(cableLine);
                        acTrans.AddNewlyCreatedDBObject(cableLine, true);

                        #region switch
                        if (!unit.consumerName.Contains("Ввод"))
                        {
                            ids = new ObjectIdCollection();
                            filename = @"Data\Consumer.dwg";
                            blockName = "Consumer";
                            using (Database sourceDb = new Database(false, true))
                            {
                                if (System.IO.File.Exists(filename))
                                {
                                    sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                                    using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                                    {
                                        BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                                        if (bt.Has(blockName))
                                            ids.Add(bt[blockName]);
                                        trans.Commit();
                                    }
                                }
                                else editor.WriteMessage("Не найден файл {0}", filename);
                                if (ids.Count > 0)
                                {
                                    acTrans.TransactionManager.QueueForGraphicsFlush();
                                    IdMapping iMap = new IdMapping();
                                    acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                                    BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                                    if (bt.Has(blockName))
                                    {
                                        BlockReference br = new BlockReference(cableLine.EndPoint, bt[blockName]);
                                        br.Layer = "0";
                                        modSpace.AppendEntity(br);
                                        acTrans.AddNewlyCreatedDBObject(br, true);
                                        if (br.IsDynamicBlock)
                                        {
                                            DynamicBlockReferencePropertyCollection props =
                                                br.DynamicBlockReferencePropertyCollection;

                                            foreach (DynamicBlockReferenceProperty prop in props)
                                            {
                                                object[] values = prop.GetAllowedValues();
                                                if (prop.PropertyName == "Видимость1" && !prop.ReadOnly)
                                                {
                                                    if (unit.planName.Contains("AH") || unit.planName.Contains("BL") || unit.planName.Contains("FNM") || unit.planName.Contains("P"))
                                                        prop.Value = values[0];
                                                    else if (unit.planName.Contains("EK"))
                                                        prop.Value = values[1];
                                                    else if (unit.planName.Contains("FH") || unit.planName.Contains("WW") || unit.planName.Contains("FF") || unit.planName.Contains("FK") || unit.planName.Contains("FJ") || unit.planName.Contains("HPL") || unit.planName.Contains("VA"))
                                                        prop.Value = values[2];
                                                    else if (unit.planName.Contains("EL"))
                                                        prop.Value = values[3];
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                            }
                        }
                        #endregion
                    }
                }
                else
                {
                    currentPoint = currentPoint.Add(new Vector3d(100, 0, 0));
                    Line avrLine = new Line();
                    avrLine.SetDatabaseDefaults();
                    avrLine.Layer = "ER-DWG";
                    avrLine.StartPoint = lowestPoint;
                    avrLine.EndPoint = new Point3d(currentPoint.X, lowestPoint.Y, 0);
                    modSpace.AppendEntity(avrLine);
                    acTrans.AddNewlyCreatedDBObject(avrLine, true);

                    if (currentPoint.Y > avrLine.StartPoint.Y)
                    {
                        Line avrLineUp = new Line();
                        avrLineUp.SetDatabaseDefaults();
                        avrLineUp.Layer = "ER-DWG";
                        avrLineUp.StartPoint = avrLine.EndPoint;
                        avrLineUp.EndPoint = avrLineUp.StartPoint.Add(new Vector3d(0, currentPoint.Y - avrLine.StartPoint.Y, 0));
                        modSpace.AppendEntity(avrLineUp);
                        acTrans.AddNewlyCreatedDBObject(avrLineUp, true);
                    }

                    #region AVR
                    ids = new ObjectIdCollection();
                    filename = @"Data\Automatic.dwg";
                    blockName = "Automatic";
                    using (Database sourceDb = new Database(false, true))
                    {
                        if (System.IO.File.Exists(filename))
                        {
                            sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                            using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                            {
                                BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                                if (bt.Has(blockName))
                                    ids.Add(bt[blockName]);
                                trans.Commit();
                            }
                        }
                        else editor.WriteMessage("Не найден файл {0}", filename);
                        if (ids.Count > 0)
                        {
                            acTrans.TransactionManager.QueueForGraphicsFlush();
                            IdMapping iMap = new IdMapping();
                            acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                            BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                            if (bt.Has(blockName))
                            {
                                BlockReference br = new BlockReference(currentPoint, bt[blockName]);
                                br.Layer = "0";
                                modSpace.AppendEntity(br);
                                acTrans.AddNewlyCreatedDBObject(br, true);
                                DynamicBlockReferenceProperty property = null;
                                object value = null;
                                if (br.IsDynamicBlock)
                                {
                                    DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
                                    foreach (DynamicBlockReferenceProperty prop in props)
                                    {
                                        object[] values = prop.GetAllowedValues();
                                        if (prop.PropertyName == "Видимость1" && !prop.ReadOnly)
                                        {
                                            prop.Value = values[42];
                                            value = values[42];
                                            property = prop;
                                            break;
                                        }
                                    }
                                }
                                BlockTableRecord btr = bt[blockName].GetObject(OpenMode.ForWrite) as BlockTableRecord;
                                foreach (ObjectId id in btr)
                                {
                                    DBObject obj = id.GetObject(OpenMode.ForWrite);
                                    AttributeDefinition attDef = obj as AttributeDefinition;
                                    if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                                    {
                                        #region attributes
                                        switch (attDef.Tag)
                                        {
                                            case "ФАЗА1":
                                                {
                                                    using (AttributeReference attRef = new AttributeReference())
                                                    {
                                                        attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                                        attRef.TextString = unit.phases.Contains("/") ? unit.phases.Split('/')[1] : "-";
                                                        br.AttributeCollection.AppendAttribute(attRef);
                                                        acTrans.AddNewlyCreatedDBObject(attRef, true);
                                                    }
                                                    break;
                                                }
                                        }
                                        #endregion
                                    }
                                }
                                getSize(br, ref minPoint, ref maxPoint);
                                while (minPoint.X < prevAutomatic.X)
                                {
                                    br.Position = br.Position.Add(new Vector3d(1, 0, 0));
                                    minPoint = minPoint.Add(new Vector3d(1, 0, 0));
                                    maxPoint = maxPoint.Add(new Vector3d(1, 0, 0));
                                }
                                if (br.IsDynamicBlock)
                                {
                                    br.ResetBlock();
                                    property.Value = value;
                                }
                                if (maxPoint.X >= currentSheetPoint.X + 210 * (curSheetsNumber - 1))
                                {
                                    if (curSheetsNumber < maxSheets)
                                    {
                                        curSheetsNumber++;
                                        curSheet.Value = curSheet.GetAllowedValues()[curSheetsNumber - 1];
                                        editor.Regen();
                                    }
                                    else
                                    {
                                        acTrans.Abort();
                                        aborted = true;
                                        return;
                                    }
                                }
                                prevAutomatic = maxPoint;
                            }
                        }
                        else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
                    }
                    #endregion

                    currentSection++;
                    sinFilter = 1;
                }
                currentPoint = currentPoint.Add(new Vector3d(maxPoint.X - minPoint.X, 0, 0));
                acTrans.Commit();
            }
        }

        static private void getSize(BlockReference bR, ref Point3d min, ref Point3d max)
        {
            DBObjectCollection objects = new DBObjectCollection();
            bR.Explode(objects);
            Extents3d ext = new Extents3d();
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Bounds != null)
                {
                    Entity ent = objects[i] as Entity;
                    if (ent.Visible != false)
                        ext.AddExtents(objects[i].Bounds.Value);
                }
                else
                {
                    DBObject obj = objects[i];
                    AttributeDefinition attDef = obj as AttributeDefinition;
                    if ((attDef != null) && (!attDef.Constant) && attDef.Visible == true)
                    {
                        Point3d point = attDef.Position;
                        if (attDef.Justify == AttachmentPoint.BaseRight) point = point.Add(new Vector3d(-2 * attDef.TextString.Length, 0, 0));
                        else if (attDef.Justify == AttachmentPoint.BaseLeft) point = point.Add(new Vector3d(2 * attDef.TextString.Length, 0, 0));
                        ext.AddPoint(point);
                    }
                }
            }
            objects.Dispose();
            min = ext.MinPoint;
            max = ext.MaxPoint;
        }

        static private  Point3d getLowestPoint(BlockReference bR)
        {
            DBObjectCollection objects = new DBObjectCollection();
            bR.Explode(objects);
            Point3d point = new Point3d();
            bool first = true;
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i].Bounds != null)
                {
                    Entity ent = objects[i] as Entity;
                    if (ent.Visible != false)
                    {
                        if (first)
                        {
                            first = false;
                            point = objects[i].Bounds.Value.MinPoint;
                        }
                        else
                        {
                            if (Math.Round(objects[i].Bounds.Value.MinPoint.Y, 4) < Math.Round(point.Y, 4))
                                point = objects[i].Bounds.Value.MinPoint;
                            else if (Math.Round(objects[i].Bounds.Value.MinPoint.Y, 4) == Math.Round(point.Y, 4))
                                if (Math.Round(objects[i].Bounds.Value.MinPoint.X, 4) < Math.Round(point.X, 4))
                                    point = objects[i].Bounds.Value.MinPoint;
                        }
                    }
                }
            }
            objects.Dispose();
            return point;
        }

        private static void insertSheet(Transaction acTrans, BlockTableRecord modSpace, Database acdb, Point3d point)
        {
            ObjectIdCollection ids = new ObjectIdCollection();
            string filename;
            string blockName;
            if (currentSheetNumber==1)
            {
                filename = @"Data\FrameSingle1.dwg";
                blockName = "FrameSingle1";
            }
            else
            {
                filename = @"Data\FrameSingle1.dwg";
                blockName = "FrameSingle1";
            }
            using (Database sourceDb = new Database(false, true))
            {
                if (System.IO.File.Exists(filename))
                {
                    sourceDb.ReadDwgFile(filename, System.IO.FileShare.Read, true, "");
                    using (Transaction trans = sourceDb.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)trans.GetObject(sourceDb.BlockTableId, OpenMode.ForRead);
                        if (bt.Has(blockName))
                            ids.Add(bt[blockName]);
                        trans.Commit();
                    }
                }
                else editor.WriteMessage("Не найден файл {0}", filename);
                if (ids.Count > 0)
                {
                    acTrans.TransactionManager.QueueForGraphicsFlush();
                    IdMapping iMap = new IdMapping();
                    acdb.WblockCloneObjects(ids, acdb.CurrentSpaceId, iMap, DuplicateRecordCloning.Replace, false);
                    BlockTable bt = (BlockTable)acTrans.GetObject(acdb.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(blockName))
                    {
                        BlockReference br = new BlockReference(point, bt[blockName]);
                        modSpace.AppendEntity(br);
                        acTrans.AddNewlyCreatedDBObject(br, true);
                        if (br.IsDynamicBlock)
                        {
                            DynamicBlockReferencePropertyCollection props =
                                br.DynamicBlockReferencePropertyCollection;
                            foreach (DynamicBlockReferenceProperty prop in props)
                            {
                                object[] values = prop.GetAllowedValues();
                                if (prop.PropertyName == "Выбор1" && !prop.ReadOnly)
                                {
                                    prop.Value = values[curSheetsNumber-1];
                                    curSheet = prop;
                                    break;
                                }
                            }
                        }
                    }
                }
                else editor.WriteMessage("В файле не найден блок с именем \"{0}\"", blockName);
            }
        }
    }
}