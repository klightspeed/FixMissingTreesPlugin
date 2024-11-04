using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.WorldEnvironment;
using Sandbox.ModAPI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DebugSETrees
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class DebugSETrees : MySessionComponentBase
    {
        private const ushort NETWORK_ID = 0x854c;
        private const int UPDATE_FRAME_COUNT = 10;
        private const double SEARCH_RADIUS = 50000;
        private const double MAX_ITEM_DIST = 2000;
        private const double MAX_DRAW_DIST = 500;
        private const double MIN_DRAW_DIST = 2;

        private bool IsInitialized;
        private int FrameCount;
        private bool Processing = false;

        private readonly Dictionary<MyTuple<long, long>, MyEntity> Entities = new Dictionary<MyTuple<long, long>, MyEntity>();
        private readonly Dictionary<long, List<MyEntity>> PlayerEntities = new Dictionary<long, List<MyEntity>>();
        private readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentSector> CurrentSectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentSector> Sectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentSector> AddSectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentSector> DelSectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();

        private readonly Dictionary<MyTuple<long, long>, EnvironmentSector> XmlSectors = new Dictionary<MyTuple<long, long>, EnvironmentSector>();

        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentSector> ServerSectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();
        private readonly Dictionary<long, Dictionary<MyTuple<long, long, int>, EnvironmentSector>> ClientSectors = new Dictionary<long, Dictionary<MyTuple<long, long, int>, EnvironmentSector>>();

        private readonly Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> ServerItems = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentItem> ServerItems2 = new Dictionary<MyTuple<long, long, int>, EnvironmentItem>();
        private readonly Dictionary<long, Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>> ClientItems = new Dictionary<long, Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>>();
        private readonly Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> Items = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
        private readonly Dictionary<MyTuple<long, long, int>, EnvironmentItem> Items2 = new Dictionary<MyTuple<long, long, int>, EnvironmentItem>();
        private readonly Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> CurrentItems = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
        private readonly Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> AddItems = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
        private readonly Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> DelItems = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
        private readonly Dictionary<long, Dictionary<int, MyPhysicalModelDefinition>> ModelDefinitions = new Dictionary<long, Dictionary<int, MyPhysicalModelDefinition>>();
        private readonly Dictionary<string, double> ModelSizes = new Dictionary<string, double>();

        private readonly List<EnvironmentItem> DrawItems = new List<EnvironmentItem>();
        private readonly List<EnvironmentItem> DrawServerItems = new List<EnvironmentItem>();

        private List<MyTuple<Vector3D, Vector3D, Vector4, float>> DrawnLines = new List<MyTuple<Vector3D, Vector3D, Vector4, float>>();
        private List<MyTuple<Vector3D, Vector3D, Vector4, float>> DrawnLinesBuffer = new List<MyTuple<Vector3D, Vector3D, Vector4, float>>();
        private List<MyTuple<MyQuadD, Vector4>> DrawnQuads = new List<MyTuple<MyQuadD, Vector4>>();
        private List<MyTuple<MyQuadD, Vector4>> DrawnQuadsBuffer = new List<MyTuple<MyQuadD, Vector4>>();
        private List<MyTuple<MatrixD, BoundingBoxD, Color>> DrawnBoxes = new List<MyTuple<MatrixD, BoundingBoxD, Color>>();
        private List<MyTuple<MatrixD, BoundingBoxD, Color>> DrawnBoxesBuffer = new List<MyTuple<MatrixD, BoundingBoxD, Color>>();

        private TextWriter _logfile;
        private string _logfilename;

        private readonly object _loggerLock = new object();

        private void Log(string str)
        {
            lock (_loggerLock)
            {
                if (_logfile == null)
                {
                    _logfilename = $"{typeof(DebugSETrees).Name}-{DateTime.Now:yyyyMMddHHmmss}.log";
                    _logfile = MyAPIGateway.Utilities.WriteFileInLocalStorage(_logfilename, typeof(DebugSETrees));
                }

                _logfile.WriteLine(str);
                _logfile.Flush();
            }
        }

        public override void Draw()
        {
            var lineMaterial = MyStringId.GetOrCompute("Square");
            var faceMaterial = MyStringId.GetOrCompute("ContainerBorder");

            lock (((ICollection)DrawnLines).SyncRoot)
            {
                foreach (var line in DrawnLines)
                {
                    var colour = line.Item3;
                    MySimpleObjectDraw.DrawLine(line.Item1, line.Item2, lineMaterial, ref colour, line.Item4);
                }
            }

            lock (((ICollection)DrawnBoxes).SyncRoot)
            {
                foreach (var box in DrawnBoxes)
                {
                    var matrix = box.Item1;
                    var boundingBox = box.Item2;
                    var colour = box.Item3;

                    MySimpleObjectDraw.DrawTransparentBox(
                        ref matrix,
                        ref boundingBox,
                        ref colour,
                        MySimpleObjectRasterizer.Solid,
                        1
                    );
                }
            }

            lock (((ICollection)DrawnQuads).SyncRoot)
            {
                foreach (var q in DrawnQuads)
                {
                    var quad = q.Item1;
                    var cpos = (q.Item1.Point0 + q.Item1.Point1 + q.Item1.Point2 + q.Item1.Point3) / 4;
                    MyTransparentGeometry.AddQuad(faceMaterial, ref quad, q.Item2, ref cpos);
                }
            }
        }

        private void UpdateDrawnItemLines(Vector4 treeColour, Vector4 goodColour, Vector4 badColour, Vector4 badPosColour, Vector4 delColour, List<EnvironmentItem> drawItems, Dictionary<MyTuple<long, long, int>, EnvironmentItem> otherItems)
        {
            foreach (var item in drawItems)
            {
                Vector4 colorVector;
                EnvironmentItem otherItem;

                if (item.ItemModelIndex < 0 || !item.ItemEnabled)
                {
                    colorVector = delColour;
                }
                else if (otherItems.TryGetValue(new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber), out otherItem))
                {
                    if (item.Equals(ref otherItem))
                    {
                        colorVector = goodColour;
                    }
                    else if (Vector3D.DistanceSquared(item.ItemPosition, otherItem.ItemPosition) <= 0.25)
                    {
                        colorVector = badColour;
                    }
                    else
                    {
                        colorVector = badPosColour;
                    }
                }
                else
                {
                    colorVector = treeColour;
                }

                Color color = colorVector;
                color = color.Alpha(0.5f);
                var scale = Math.Max((float)item.ModelSize, 2.0f);

                if (item.EntityType == null)
                {
                    DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(
                        item.ItemPosition - item.ItemRotation.Up * scale,
                        item.ItemPosition + item.ItemRotation.Up * scale,
                        colorVector,
                        scale / 50f
                    ));
                }
            }
        }

        private void UpdateDrawnItemBoxes(Vector4 treeColour, Vector4 goodColour, Vector4 badColour, Vector4 badPosColour, Vector4 delColour, List<EnvironmentItem> drawItems, Dictionary<MyTuple<long, long, int>, EnvironmentItem> otherItems)
        {
            foreach (var item in drawItems)
            {
                Vector4 colorVector;
                EnvironmentItem otherItem;

                if (item.ItemModelIndex < 0 || !item.ItemEnabled)
                {
                    colorVector = delColour;
                }
                else if (otherItems.TryGetValue(new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber), out otherItem))
                {
                    if (item.Equals(ref otherItem))
                    {
                        colorVector = goodColour;
                    }
                    else if (Vector3D.DistanceSquared(item.ItemPosition, otherItem.ItemPosition) <= 0.25)
                    {
                        colorVector = badColour;
                    }
                    else
                    {
                        colorVector = badPosColour;
                    }
                }
                else
                {
                    colorVector = treeColour;
                }

                Color color = colorVector;
                color = color.Alpha(0.5f);
                var scale = Math.Max((float)item.ModelSize, 2.0f);

                if (item.EntityType != null)
                {
                    var matrix = MatrixD.CreateFromTransformScale(item.ItemRotation, item.ItemPosition, Vector3D.One);
                    var boundingBox = new BoundingBoxD
                    {
                        Min = Vector3.MinusOne * scale,
                        Max = Vector3.One * scale
                    };

                    DrawnBoxesBuffer.Add(new MyTuple<MatrixD, BoundingBoxD, Color>(
                        matrix,
                        boundingBox,
                        color
                    ));
                }
            }
        }

        private void UpdateDrawnSectorLines(Vector4 lodColour, Vector4 logicalColour, IEnumerable<EnvironmentSector> sectors)
        {
            foreach (var sector in sectors)
            {
                var colour = sector.IsLogicalSector ? logicalColour : lodColour;

                var p1 = sector.SectorBounds[0];
                var p2 = sector.SectorBounds[1];
                var p3 = sector.SectorBounds[2];
                var p4 = sector.SectorBounds[3];
                var p1a = sector.SectorBounds[4];
                var p2a = sector.SectorBounds[5];
                var p3a = sector.SectorBounds[6];
                var p4a = sector.SectorBounds[7];
                var p1b = p1a + Vector3D.Normalize(p1a - p1) * (sector.IsLogicalSector ? 500 : 600);
                var p2b = p2a + Vector3D.Normalize(p2a - p2) * (sector.IsLogicalSector ? 500 : 600);
                var p3b = p3a + Vector3D.Normalize(p3a - p3) * (sector.IsLogicalSector ? 500 : 600);
                var p4b = p4a + Vector3D.Normalize(p4a - p4) * (sector.IsLogicalSector ? 500 : 600);

                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p1, p1b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p2, p2b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p3, p3b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p4, p4b, colour, 1.0f));

                if (sector.IsLogicalSector)
                {
                    DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p1a, p2a, colour, 1.0f));
                    DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p2a, p4a, colour, 1.0f));
                    DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p4a, p3a, colour, 1.0f));
                    DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p3a, p1a, colour, 1.0f));
                }

                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p1b, p2b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p2b, p4b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p4b, p3b, colour, 1.0f));
                DrawnLinesBuffer.Add(new MyTuple<Vector3D, Vector3D, Vector4, float>(p3b, p1b, colour, 1.0f));
            }
        }

        private void UpdateDrawnSectorFaces(MyStringId faceMaterial, Vector4 colour, IEnumerable<EnvironmentSector> sectors)
        {
            foreach (var sector in sectors)
            {
                var p1 = sector.SectorBounds[0];
                var p2 = sector.SectorBounds[1];
                var p3 = sector.SectorBounds[2];
                var p4 = sector.SectorBounds[3];
                var p1a = sector.SectorBounds[4];
                var p2a = sector.SectorBounds[5];
                var p3a = sector.SectorBounds[7];
                var p4a = sector.SectorBounds[6];
                var p1b = p1a + Vector3D.Normalize(p1a - p1) * 500;
                var p2b = p2a + Vector3D.Normalize(p2a - p2) * 500;
                var p3b = p3a + Vector3D.Normalize(p3a - p3) * 500;
                var p4b = p4a + Vector3D.Normalize(p4a - p4) * 500;

                var quads = new MyQuadD[]
                {
                    new MyQuadD { Point0 = p1a, Point1 = p2a, Point2 = p4a, Point3 = p3a },
                    new MyQuadD { Point0 = p1a, Point1 = p3a, Point2 = p4a, Point3 = p2a },
                    new MyQuadD { Point0 = p1, Point1 = p2, Point2 = p2b, Point3 = p1b },
                    new MyQuadD { Point0 = p1, Point1 = p1b, Point2 = p2b, Point3 = p2 },
                    new MyQuadD { Point0 = p2, Point1 = p4, Point2 = p4b, Point3 = p2b },
                    new MyQuadD { Point0 = p2, Point1 = p2b, Point2 = p4b, Point3 = p4 },
                    new MyQuadD { Point0 = p4, Point1 = p3, Point2 = p3b, Point3 = p4b },
                    new MyQuadD { Point0 = p4, Point1 = p4b, Point2 = p3b, Point3 = p3 },
                    new MyQuadD { Point0 = p3, Point1 = p1, Point2 = p1b, Point3 = p3b },
                    new MyQuadD { Point0 = p3, Point1 = p3b, Point2 = p1b, Point3 = p1 }
                };

                foreach (MyQuadD quad in quads)
                {
                    DrawnQuadsBuffer.Add(new MyTuple<MyQuadD, Vector4>(quad, colour));
                }
            }
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NETWORK_ID, MessageHandler);
            MyAPIGateway.Utilities.MessageEnteredSender -= Utilities_MessageEnteredSender;

            IsInitialized = false;
        }

        private List<MyEntity> GetPlayerEntities(IMyPlayer player)
        {
            List<MyEntity> playerEntities;

            if (player?.Character != null)
            {
                if (!PlayerEntities.TryGetValue(player.IdentityId, out playerEntities))
                {
                    PlayerEntities[player.IdentityId] = playerEntities = new List<MyEntity>();
                }

                playerEntities.Clear();

                var character = player.Character;
                var sphere = new BoundingSphereD(character.WorldMatrix.Translation, SEARCH_RADIUS);
                MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, playerEntities);

                foreach (var entity in playerEntities)
                {
                    if (entity.Closed || entity.MarkedForClose) continue;

                    MyTuple<long, long> key;

                    if (entity is MyEnvironmentSector)
                    {
                        var sector = (MyEnvironmentSector)entity;
                        var entityId = sector.Parent.EntityId;
                        var sectorId = sector.SectorId;
                        var relpos = (Vector3)(character.WorldMatrix.Translation - sector.WorldMatrix.Translation);
                        key = new MyTuple<long, long>(entityId, sectorId);

                        if (sector.DataView == null || sector.DataView.Items.All(e => Vector3.DistanceSquared(relpos, e.Position) > MAX_ITEM_DIST * MAX_ITEM_DIST)) continue;
                    }
                    else
                    {
                        key = new MyTuple<long, long>(entity.EntityId, 0);
                    }

                    if (!Entities.ContainsKey(key))
                    {
                        Entities[key] = entity;
                    }
                }

                return playerEntities;
            }

            return new List<MyEntity>();
        }

        private void SpinThread()
        {
            while (base.Loaded)
            {
                MyAPIGateway.Parallel.Sleep(100);
            }
        }

        private EnvironmentSector AddSector(long entityId, long sectorId, int lodLevel, Vector3D sectorPosition, Vector3D[] bounds, bool isLogicalSector)
        {
            var keySectorId = sectorId | (isLogicalSector ? 0 : 0x1000000000000000L);

            var sectorkey = new MyTuple<long, long, int>(entityId, keySectorId, lodLevel);

            var newsector = new EnvironmentSector
            {
                EntityId = entityId,
                LogicalSectorId = sectorId,
                LodLevel = lodLevel,
                SectorPosition = sectorPosition,
                SectorBounds = bounds.ToArray(),
                IsLogicalSector = isLogicalSector,
                SectorX = (int)(sectorId & 0xFFFFFF),
                SectorY = (int)((sectorId >> 24) & 0xFFFFFF),
                SectorFace = (int)((sectorId >> 48) & 0x07),
                SectorLod = (int)((sectorId >> 51) & 0xFF)
            };

            CurrentSectors[sectorkey] = newsector;

            EnvironmentSector oldsector;

            if (!Sectors.TryGetValue(sectorkey, out oldsector) || !newsector.Equals(ref oldsector))
            {
                Sectors[sectorkey] = newsector;
                AddSectors[sectorkey] = newsector;
            }

            return newsector;
        }

        private class SectorEnumeratorHelper
        {
            public EnvironmentSector SectorInfo;
            public DebugSETrees DebugSETrees;
            public Dictionary<int, MyPhysicalModelDefinition> ModelDefs;
            public IMyEnvironmentOwner EnvironmentOwner;

            public SectorEnumeratorHelper(EnvironmentSector sectorInfo, DebugSETrees debugSETrees, MyEnvironmentSector sector)
            {
                SectorInfo = sectorInfo;
                DebugSETrees = debugSETrees;
                EnvironmentOwner = sector.Owner;

                if (!debugSETrees.ModelDefinitions.TryGetValue(sectorInfo.EntityId, out ModelDefs))
                {
                    debugSETrees.ModelDefinitions[sectorInfo.EntityId] = ModelDefs = new Dictionary<int, MyPhysicalModelDefinition>();
                }
            }

            public void EnumerateItems(int itemnum, ref ItemInfo item)
            {
                MyPhysicalModelDefinition modeldef;

                var modelIndex = item.ModelIndex < 0 ? ~item.ModelIndex : item.ModelIndex;

                if (!ModelDefs.TryGetValue(modelIndex, out modeldef))
                {
                    try
                    {
                        modeldef = EnvironmentOwner.GetModelForId(item.ModelIndex);
                    }
                    catch
                    {
                        modeldef = null;
                    }

                    ModelDefs[item.ModelIndex] = modeldef;
                }

                var newitem = new EnvironmentItem
                {
                    EntityId = SectorInfo.EntityId,
                    LogicalSectorId = SectorInfo.LogicalSectorId,
                    LodLevel = SectorInfo.LodLevel,
                    ItemNumber = itemnum,
                    ItemEnabled = item.IsEnabled,
                    ItemPosition = (Vector3D)item.Position + SectorInfo.SectorPosition,
                    SectorRelativePosition = item.Position,
                    ItemRotation = item.Rotation,
                    ItemDefinitionIndex = item.DefinitionIndex,
                    ItemModelIndex = item.ModelIndex,
                    ItemModelName = modeldef?.Model,
                    ItemModelType = modeldef?.Id.TypeId.ToString(),
                    ItemModelSubtype = modeldef?.Id.SubtypeName,
                    ModelSize = modeldef?.Id.TypeId.ToString() == "MyObjectBuilder_TreeDefinition" ? 10f : 2f,
                    ModelMass = modeldef?.Mass ?? 0f
                };

                var itemkey = new MyTuple<long, long, int, int>(SectorInfo.EntityId, SectorInfo.LogicalSectorId, SectorInfo.LodLevel, itemnum);

                DebugSETrees.CurrentItems[itemkey] = newitem;

                EnvironmentItem olditem;

                if (!DebugSETrees.Items.TryGetValue(itemkey, out olditem)
                    || !olditem.Equals(ref newitem))
                {
                    DebugSETrees.Items[itemkey] = newitem;
                    DebugSETrees.AddItems[itemkey] = newitem;
                }
            }
        }

        private void ProcessSector(MyEnvironmentSector sector)
        {
            var entityId = sector.Parent.EntityId;
            var sectorId = sector.SectorId;
            var dataview = sector.DataView;

            if (dataview == null) return;

            AddSector(entityId, sectorId, sector.LodLevel, sector.SectorCenter, sector.Bounds, false);

            foreach (var logSector in dataview.LogicalSectors)
            {
                var sectorInfo = AddSector(entityId, logSector.Id, sector.LodLevel, logSector.WorldPos, logSector.Bounds, true);
                logSector.IterateItems(new SectorEnumeratorHelper(sectorInfo, this, sector).EnumerateItems);
            }
        }

        private void ProcessBoulder(MyVoxelBase voxel)
        {
            if (voxel.BoulderInfo == null) return;

            var boulderinfo = voxel.BoulderInfo.Value;

            var newitem = new EnvironmentItem
            {
                EntityId = boulderinfo.PlanetId,
                EntityType = voxel.GetType().Name,
                LogicalSectorId = boulderinfo.SectorId,
                LodLevel = -2,
                ItemNumber = boulderinfo.ItemId,
                ItemEnabled = true,
                ItemPosition = voxel.PositionComp.WorldVolume.Center,
                ItemRotation = Quaternion.CreateFromRotationMatrix(voxel.WorldMatrix.GetOrientation()),
                ModelSize = voxel.PositionComp.WorldVolume.Radius,
            };

            var itemkey = new MyTuple<long, long, int, int>(newitem.EntityId, newitem.LogicalSectorId, newitem.LodLevel, newitem.ItemNumber);

            CurrentItems[itemkey] = newitem;

            EnvironmentItem olditem;

            if (!Items.TryGetValue(itemkey, out olditem)
                || !olditem.Equals(ref newitem))
            {
                Items[itemkey] = newitem;
                AddItems[itemkey] = newitem;
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (!IsInitialized)
            {
                Log($"Space Engineers {MyAPIGateway.Session.Version}");
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, MessageHandler);
                MyAPIGateway.Utilities.MessageEnteredSender += Utilities_MessageEnteredSender;
                MyAPIGateway.Parallel.StartBackground(SpinThread);
                IsInitialized = true;
            }

            if ((FrameCount % UPDATE_FRAME_COUNT) == 0 && !Processing)
            {
                Processing = true;

                Entities.Clear();

                if (MyAPIGateway.Session.IsServer)
                {
                    Players.Clear();
                    MyAPIGateway.Multiplayer.Players.GetPlayers(Players, e => e.IsBot == false && e.SteamUserId != 0);

                    foreach (var player in Players)
                    {
                        if (!player.IsBot)
                        {
                            GetPlayerEntities(player);
                        }
                    }
                }
                else if (MyAPIGateway.Session.Player != null)
                {
                    GetPlayerEntities(MyAPIGateway.Session.Player);
                }

                MyAPIGateway.Parallel.Start(() =>
                {
                    try
                    {
                        ProcessSectors();
                    }
                    catch (Exception ex)
                    {
                        Log($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] Error processing sectors: {ex}");
                    }
                    finally
                    {
                        Processing = false;
                    }
                });
            }

            FrameCount++;
        }

        private void ProcessSectors()
        {
            CurrentSectors.Clear();
            AddSectors.Clear();
            DelSectors.Clear();
            CurrentItems.Clear();
            AddItems.Clear();
            DelItems.Clear();

            foreach (var entity in Entities.Values)
            {
                if (entity.Closed || entity.MarkedForClose) continue;

                if (entity is MyEnvironmentSector)
                {
                    ProcessSector((MyEnvironmentSector)entity);
                }
                else if (entity.EntityId != 0 && entity is MyVoxelBase)
                {
                    ProcessBoulder((MyVoxelBase)entity);
                }
            }

            foreach (var kvp in Sectors)
            {
                if (!CurrentSectors.ContainsKey(kvp.Key))
                {
                    DelSectors[kvp.Key] = kvp.Value;
                }
            }

            lock (((ICollection)Sectors).SyncRoot)
            {
                foreach (var kvp in DelSectors)
                {
                    Sectors.Remove(kvp.Key);
                }
            }

            foreach (var kvp in Items)
            {
                if (!CurrentItems.ContainsKey(kvp.Key))
                {
                    DelItems[kvp.Key] = kvp.Value;
                }
            }

            foreach (var kvp in DelItems)
            {
                Items.Remove(kvp.Key);
            }

            Items2.Clear();

            foreach (var item in Items.Values)
            {
                Items2[new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber)] = item;
            }

            foreach (var sector in AddSectors.Values)
            {
                LogAdd("Local", sector);
            }

            foreach (var sector in DelSectors.Values)
            {
                LogDelete("Local", sector);
            }

            foreach (var item in AddItems.Values)
            {
                LogAdd("Local", item);
            }

            foreach (var item in DelItems.Values)
            {
                LogDelete("Local", item);
            }

            SendData();
            SaveEnvironmentItems();
            UpdateDrawnItems();
        }

        private void SaveEnvironmentItems()
        {
            var sectors = Sectors.Values.Where(e => e.IsLogicalSector).ToArray();
            var items =
                Items
                    .Values
                    .GroupBy(e => new MyTuple<long, long>(e.EntityId, e.LogicalSectorId))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(e => e.ItemNumber)
                              .ThenBy(e => e.LodLevel)
                              .ToArray()
                    );

            var serverItems =
                ServerItems
                    .Values
                    .GroupBy(e => new MyTuple<long, long>(e.EntityId, e.LogicalSectorId))
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(e => e.ItemNumber)
                              .ThenBy(e => e.LodLevel)
                              .ToArray()
                    );

            for (int i = 0; i < sectors.Length; i++)
            {
                var sector = sectors[i];
                EnvironmentSector xmlsector;

                if (!XmlSectors.TryGetValue(new MyTuple<long, long>(sector.EntityId, sector.LogicalSectorId), out xmlsector))
                {
                    xmlsector = sector;
                    xmlsector.Items = new List<EnvironmentItem>();
                }

                EnvironmentItem[] sectorItems;

                bool update = false;

                if (items.TryGetValue(new MyTuple<long, long>(sector.EntityId, sector.LogicalSectorId), out sectorItems))
                {
                    ProcessXmlItems(ref sectorItems, ref xmlsector, ref update, false);
                }

                if (serverItems.TryGetValue(new MyTuple<long, long>(sector.EntityId, sector.LogicalSectorId), out sectorItems))
                {
                    ProcessXmlItems(ref sectorItems, ref xmlsector, ref update, true);
                }

                xmlsector.Items.SortNoAlloc((l, r) => l.ItemNumber.CompareTo(r.ItemNumber));

                if (update)
                {
                    var sectorFilename = $"Sector-{sector.EntityId}-{sector.LogicalSectorId}.xml";

                    using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(sectorFilename, typeof(DebugSETrees)))
                    {
                        writer.Write(MyAPIGateway.Utilities.SerializeToXML(xmlsector));
                    }

                    XmlSectors[new MyTuple<long, long>(sector.EntityId, sector.LogicalSectorId)] = xmlsector;
                }
            }
        }

        private static void ProcessXmlItems(ref EnvironmentItem[] sectorItems, ref EnvironmentSector xmlsector, ref bool update, bool fromServer)
        {
            for (int j = 0; j < sectorItems.Length; j++)
            {
                var item = sectorItems[j];
                item.FromServer = fromServer;

                if (item.LodLevel >= 0 || fromServer)
                {
                    var xmlidx = xmlsector.Items.FindIndex(e => e.Equals(ref item, 0.015625));

                    if (xmlidx < 0)
                    {
                        item.SeenLods = new List<int> { item.LodLevel };
                        item.MinLod = item.MaxLod = item.LodLevel;
                        xmlsector.Items.Add(item);
                        update = true;
                    }
                    else
                    {
                        var xmlitem = xmlsector.Items[xmlidx];

                        if (fromServer && xmlitem.FromServer == false)
                        {
                            xmlitem.FromServer = true;
                            update = true;
                        }

                        if (!xmlitem.SeenLods.Contains(item.LodLevel))
                        {
                            xmlitem.SeenLods.Add(item.LodLevel);
                        }

                        if (item.LodLevel < xmlitem.MinLod)
                        {
                            xmlitem.MinLod = item.LodLevel;
                            update = true;
                        }

                        if (item.LodLevel > xmlitem.MaxLod)
                        {
                            xmlitem.MaxLod = item.LodLevel;
                            update = true;
                        }

                        xmlsector.Items[xmlidx] = xmlitem;
                    }
                }
            }
        }

        private void UpdateDrawnItems()
        {
            var player = MyAPIGateway.Session.LocalHumanPlayer;

            if (player?.Character != null)
            {
                var playerpos = player.Character.WorldMatrix.Translation;

                DrawItems.Clear();

                foreach (var kvp in Items)
                {
                    if (kvp.Key.Item1 != player.Character.EntityId)
                    {
                        var dist = Vector3D.Distance(playerpos, kvp.Value.ItemPosition);

                        if (dist > MIN_DRAW_DIST && dist < MAX_DRAW_DIST)
                        {
                            var item = kvp.Value;
                            item.DistanceFromPlayer = dist;
                            DrawItems.Add(item);
                        }
                    }
                }

                DrawItems.SortNoAlloc((l, r) => l.DistanceFromPlayer.CompareTo(r.DistanceFromPlayer));

                DrawServerItems.Clear();

                lock (((ICollection)ServerItems2).SyncRoot)
                {
                    foreach (var kvp in ServerItems2)
                    {
                        var dist = Vector3D.Distance(playerpos, kvp.Value.ItemPosition);

                        if (dist > MIN_DRAW_DIST && dist < MAX_DRAW_DIST)
                        {
                            var item = kvp.Value;
                            item.DistanceFromPlayer = dist;

                            EnvironmentItem citem;

                            if (!Items2.TryGetValue(kvp.Key, out citem) || Vector3D.DistanceSquared(item.ItemPosition, citem.ItemPosition) > 0.25)
                            {
                                DrawServerItems.Add(item);
                            }
                        }
                    }
                }

                DrawServerItems.SortNoAlloc((l, r) => l.DistanceFromPlayer.CompareTo(r.DistanceFromPlayer));

                var lineMaterial = MyStringId.GetOrCompute("Square");
                var faceMaterial = MyStringId.GetOrCompute("ContainerBorder");
                var clientTreeColour = Color.Aqua.ToVector4();
                var clientGoodColour = Color.Green.ToVector4();
                var clientBadColour = Color.Red.ToVector4();
                var clientBadPosColour = Color.Pink.ToVector4();
                var clientDelColour = Color.Yellow.ToVector4();
                var serverTreeColour = Color.Orange.ToVector4();
                var serverGoodColour = Color.Green.ToVector4();
                var serverBadColour = Color.Purple.ToVector4();
                var serverBadPosColour = Color.Magenta.ToVector4();
                var serverDelColour = Color.Maroon.ToVector4();

                lock (((ICollection)DrawnLinesBuffer).SyncRoot)
                {
                    DrawnLinesBuffer.Clear();
                    UpdateDrawnItemLines(clientTreeColour, clientGoodColour, clientBadColour, clientBadPosColour, clientDelColour, DrawItems, ServerItems2);
                    UpdateDrawnItemLines(serverTreeColour, serverGoodColour, serverBadColour, serverBadPosColour, serverDelColour, DrawServerItems, Items2);
                    UpdateDrawnSectorLines(clientTreeColour, clientGoodColour, Sectors.Values);
                }

                lock (((ICollection)DrawnLines).SyncRoot)
                {
                    var linestmp = DrawnLines;
                    DrawnLines = DrawnLinesBuffer;
                    DrawnLinesBuffer = linestmp;
                }

                lock (((ICollection)DrawnBoxesBuffer).SyncRoot)
                {
                    DrawnBoxesBuffer.Clear();
                    UpdateDrawnItemBoxes(clientTreeColour, clientGoodColour, clientBadColour, clientBadPosColour, clientDelColour, DrawItems, ServerItems2);
                    UpdateDrawnItemBoxes(serverTreeColour, serverGoodColour, serverBadColour, serverBadPosColour, serverDelColour, DrawServerItems, Items2);
                }

                lock (((ICollection)DrawnBoxes).SyncRoot)
                {
                    var boxestmp = DrawnBoxes;
                    DrawnBoxes = DrawnBoxesBuffer;
                    DrawnBoxesBuffer = boxestmp;
                }

                lock (((ICollection)DrawnQuadsBuffer).SyncRoot)
                {
                    DrawnQuadsBuffer.Clear();
                    UpdateDrawnSectorFaces(faceMaterial, clientGoodColour, ServerSectors.Values);
                }

                lock (((ICollection)DrawnQuads).SyncRoot)
                {
                    var quadstmp = DrawnQuads;
                    DrawnQuads = DrawnQuadsBuffer;
                    DrawnQuadsBuffer = quadstmp;
                }
            }
        }

        private void SendData()
        {
            if (MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                var envdata = new EnvironmentData
                {
                    AddItems = AddItems.Values.ToList(),
                    DeleteItems = DelItems.Values.ToList()
                };

                if ((FrameCount % (UPDATE_FRAME_COUNT * 10)) == 0)
                {
                    envdata.Items = CurrentItems.Values.ToList();
                }

                if (MyAPIGateway.Session.IsServer)
                {
                    envdata.FromServer = true;
                }
                else if (MyAPIGateway.Session.LocalHumanPlayer != null)
                {
                    envdata.IdentityId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
                }

                var data = MyAPIGateway.Utilities.SerializeToBinary(envdata);
                var reqdata = MyCompression.Compress(data);
                var msgdata = new byte[reqdata.Length + 12];
                Array.Copy(Encoding.ASCII.GetBytes("TREEDATA"), msgdata, 8);
                Array.Copy(BitConverter.GetBytes(reqdata.Length), 0, msgdata, 8, 4);
                Array.Copy(reqdata, 0, msgdata, 12, reqdata.Length);

                MyAPIGateway.Multiplayer.SendMessageToOthers(NETWORK_ID, msgdata);
            }
        }

        private void LogDelete(string source, EnvironmentSector sector)
        {
            Log(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] {source}: Remove Sector {sector.EntityId}:{sector.LogicalSectorId}:{sector.LodLevel}"
            );
        }

        private void LogAdd(string source, EnvironmentSector sector)
        {
            Log(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] {source}: Add Sector {sector.EntityId}:{sector.LogicalSectorId}:{sector.LodLevel}:\n" +
                $"    Position: {sector.SectorPosition.X}, {sector.SectorPosition.Y}, {sector.SectorPosition.Z}\n" +
                string.Join("\n", sector.SectorBounds.Select((b, i) => $"    Bounds[{i}]: {b.X}, {b.Y}, {b.Z}"))
            );
        }

        private void LogDelete(string source, EnvironmentItem item)
        {
            Log(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] {source}: Remove Item {item.EntityId}:{item.LogicalSectorId}:{item.LodLevel}:{item.ItemNumber}"
            );
        }

        private void LogAdd(string source, EnvironmentItem item)
        {
            Log(
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fffffff}] {source}: Add Item {item.EntityId}:{item.LogicalSectorId}:{item.LodLevel}:{item.ItemNumber}:\n" +
                $"    ModelIndex: {item.ItemModelIndex}\n" +
                $"    ModelName: {item.ItemModelName}\n" +
                $"    ModelType: {item.ItemModelType}\n" +
                $"    ModelSubtype: {item.ItemModelSubtype}\n" +
                $"    DefinitionIndex: {item.ItemDefinitionIndex}\n" +
                $"    Position: {item.ItemPosition.X}, {item.ItemPosition.Y}, {item.ItemPosition.Z}"
            );
        }

        private void Utilities_MessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (messageText == "/raycast" && MyAPIGateway.Session.Camera != null)
            {
                sendToOthers = false;

                var camera = MyAPIGateway.Session.Camera;
                if (camera != null)
                {
                    var camerapos = camera.Position;
                    var camerafwd = camera.WorldMatrix.Forward;
                    var hits = new List<IHitInfo>();

                    MyAPIGateway.Physics.CastRay(camerapos, camerapos + camerafwd * 500, hits, MyAPIGateway.Physics.GetCollisionLayer("CharacterCollisionLayer"));

                    var sectors = new HashSet<MyTuple<long, long>>();
                    var items = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();

                    foreach (var hit in hits)
                    {
                        var hitent = hit.HitEntity;
                        var hitsect = hit.HitEntity as MyEnvironmentSector;

                        if (hitent != null && hitent.Parent != null && hitent.Parent is MyPlanet && hitsect != null)
                        {
                            var planet = (MyPlanet)hitent.Parent;
                            var descsb = new StringBuilder();

                            descsb.AppendLine($"EntityId: {hitent.Parent.EntityId}");
                            descsb.AppendLine($"SectorId: {hitsect.SectorId}");

                            var gpsent = MyAPIGateway.Session.GPS.Create("Hit", descsb.ToString(), hit.Position, true, true);
                            gpsent.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(30);
                            MyAPIGateway.Session.GPS.AddLocalGps(gpsent);

                            sectors.Add(new MyTuple<long, long>(hitent.Parent.EntityId, hitsect.SectorId));

                            var surfacepos = planet.GetClosestSurfacePointGlobal(hit.Position);

                            foreach (var kvp in Items)
                            {
                                if (Vector3D.DistanceSquared(surfacepos, kvp.Value.ItemPosition) < 2500)
                                {
                                    items[kvp.Key] = kvp.Value;
                                }
                            }
                        }
                    }

                    foreach (var item in items.Values)
                    {
                        var descsb = new StringBuilder();

                        descsb.AppendLine($"EntityId: {item.EntityId}");
                        descsb.AppendLine($"SectorId: {item.LogicalSectorId}");
                        descsb.AppendLine($"ItemIndex: {item.ItemNumber}");
                        descsb.AppendLine($"DefinitionIndex: {item.ItemDefinitionIndex}");
                        descsb.AppendLine($"Model: {item.ItemModelName} [{item.ItemModelIndex}]");
                        descsb.AppendLine($"ModelType: {item.ItemModelType}");
                        descsb.AppendLine($"ModelSubtype: {item.ItemModelSubtype}");

                        EnvironmentItem serverItem;

                        if (ServerItems2.TryGetValue(new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber), out serverItem))
                        {
                            descsb.AppendLine();
                            descsb.AppendLine("Server:");

                            if (Vector3D.DistanceSquared(serverItem.ItemPosition, item.ItemPosition) > 1e-6)
                            {
                                descsb.AppendLine($"Position: {serverItem.ItemPosition.X}, {serverItem.ItemPosition.Y}, {serverItem.ItemPosition.Z}");
                            }

                            descsb.AppendLine($"DefinitionIndex: {serverItem.ItemDefinitionIndex}");
                            descsb.AppendLine($"Model: {serverItem.ItemModelName} [{serverItem.ItemModelIndex}]");
                            descsb.AppendLine($"ModelType: {serverItem.ItemModelType}");
                            descsb.AppendLine($"ModelSubtype: {serverItem.ItemModelSubtype}");
                        }

                        var gpsent = MyAPIGateway.Session.GPS.Create(item.ItemModelSubtype ?? "Tree", descsb.ToString(), item.ItemPosition, true, true);
                        gpsent.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(30);
                        MyAPIGateway.Session.GPS.AddLocalGps(gpsent);
                    }

                }
            }
            else if (messageText == "/nearbyItems" && MyAPIGateway.Session.Camera != null)
            {
                foreach (var item in Items.Values)
                {
                    if (Vector3D.Distance(item.ItemPosition, MyAPIGateway.Session.Camera.Position) < 50)
                    {
                        var descsb = new StringBuilder();

                        descsb.AppendLine($"EntityId: {item.EntityId}");
                        descsb.AppendLine($"SectorId: {item.LogicalSectorId}");
                        descsb.AppendLine($"ItemIndex: {item.ItemNumber}");
                        descsb.AppendLine($"DefinitionIndex: {item.ItemDefinitionIndex}");
                        descsb.AppendLine($"Model: {item.ItemModelName} [{item.ItemModelIndex}]");
                        descsb.AppendLine($"ModelType: {item.ItemModelType}");
                        descsb.AppendLine($"ModelSubtype: {item.ItemModelSubtype}");

                        EnvironmentItem serverItem;

                        if (ServerItems2.TryGetValue(new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber), out serverItem))
                        {
                            descsb.AppendLine();
                            descsb.AppendLine("Server:");

                            if (Vector3D.DistanceSquared(serverItem.ItemPosition, item.ItemPosition) > 1e-6)
                            {
                                descsb.AppendLine($"Position: {serverItem.ItemPosition.X}, {serverItem.ItemPosition.Y}, {serverItem.ItemPosition.Z}");
                            }

                            descsb.AppendLine($"DefinitionIndex: {serverItem.ItemDefinitionIndex}");
                            descsb.AppendLine($"Model: {serverItem.ItemModelName} [{serverItem.ItemModelIndex}]");
                            descsb.AppendLine($"ModelType: {serverItem.ItemModelType}");
                            descsb.AppendLine($"ModelSubtype: {serverItem.ItemModelSubtype}");
                        }

                        var gpsent = MyAPIGateway.Session.GPS.Create(item.ItemModelSubtype ?? "Tree", descsb.ToString(), item.ItemPosition, true, true);
                        gpsent.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(30);
                        MyAPIGateway.Session.GPS.AddLocalGps(gpsent);
                    }
                }

                foreach (var item in ServerItems.Values)
                {
                    EnvironmentItem clientItem;

                    if (Items2.TryGetValue(new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber), out clientItem)
                        && item.Equals(ref clientItem))
                    {
                        continue;
                    }

                    if (Vector3D.Distance(item.ItemPosition, MyAPIGateway.Session.Camera.Position) < 200)
                    {
                        var descsb = new StringBuilder();

                        descsb.AppendLine($"EntityId: {item.EntityId}");
                        descsb.AppendLine($"SectorId: {item.LogicalSectorId}");
                        descsb.AppendLine($"ItemIndex: {item.ItemNumber}");
                        descsb.AppendLine($"DefinitionIndex: {item.ItemDefinitionIndex}");
                        descsb.AppendLine($"Model: {item.ItemModelName} [{item.ItemModelIndex}]");
                        descsb.AppendLine($"ModelType: {item.ItemModelType}");
                        descsb.AppendLine($"ModelSubtype: {item.ItemModelSubtype}");

                        var gpsent = MyAPIGateway.Session.GPS.Create("Server/" + (item.ItemModelSubtype ?? "Tree"), descsb.ToString(), item.ItemPosition, true, true);
                        gpsent.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(30);
                        MyAPIGateway.Session.GPS.AddLocalGps(gpsent);
                    }
                }
            }
        }

        private void UpdateItems(Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> items, EnvironmentData envdata, string source)
        {
            if (envdata.Items != null)
            {
                var keys = new HashSet<MyTuple<long, long, int, int>>();

                foreach (var item in envdata.Items)
                {
                    var key = new MyTuple<long, long, int, int>(item.EntityId, item.LogicalSectorId, item.LodLevel, item.ItemNumber);
                    keys.Add(key);
                    EnvironmentItem current;

                    if (!items.TryGetValue(key, out current) || !item.Equals(ref current))
                    {
                        LogAdd(source, item);
                    }

                    items[new MyTuple<long, long, int, int>(item.EntityId, item.LogicalSectorId, item.LodLevel, item.ItemNumber)] = item;
                }

                foreach (var kvp in items.ToList())
                {
                    if (!keys.Contains(kvp.Key))
                    {
                        LogDelete(source, kvp.Value);

                        items.Remove(kvp.Key);
                    }
                }
            }
            else
            {
                if (envdata.AddItems != null)
                {
                    foreach (var item in envdata.AddItems)
                    {
                        LogAdd(source, item);

                        items[new MyTuple<long, long, int, int>(item.EntityId, item.LogicalSectorId, item.LodLevel, item.ItemNumber)] = item;
                    }
                }

                if (envdata.DeleteItems != null)
                {
                    foreach (var item in envdata.DeleteItems)
                    {
                        LogDelete(source, item);

                        items.Remove(new MyTuple<long, long, int, int>(item.EntityId, item.LogicalSectorId, item.LodLevel, item.ItemNumber));
                    }
                }
            }
        }

        private void UpdateSectors(Dictionary<MyTuple<long, long, int>, EnvironmentSector> sectors, EnvironmentData envdata, string source)
        {
            if (envdata.Sectors != null)
            {
                var keys = new HashSet<MyTuple<long, long, int>>();

                foreach (var sector in envdata.Sectors)
                {
                    var key = new MyTuple<long, long, int>(sector.EntityId, sector.LogicalSectorId, sector.LodLevel);
                    keys.Add(key);
                    EnvironmentSector current;

                    if (!sectors.TryGetValue(key, out current) || !current.Equals(sector))
                    {
                        LogAdd(source, sector);
                    }

                    sectors[key] = sector;
                }

                foreach (var kvp in sectors.ToList())
                {
                    if (!keys.Contains(kvp.Key))
                    {
                        LogDelete(source, kvp.Value);
                        sectors.Remove(kvp.Key);
                    }
                }
            }
            else
            {
                if (envdata.AddSectors != null)
                {
                    foreach (var sector in envdata.AddSectors)
                    {
                        LogAdd(source, sector);

                        sectors[new MyTuple<long, long, int>(sector.EntityId, sector.LogicalSectorId, sector.LodLevel)] = sector;
                    }
                }

                if (envdata.DeleteSectors != null)
                {
                    foreach (var sector in envdata.DeleteSectors)
                    {
                        LogDelete(source, sector);

                        sectors.Remove(new MyTuple<long, long, int>(sector.EntityId, sector.LogicalSectorId, sector.LodLevel));
                    }
                }

            }
        }

        private void MessageHandler(byte[] msgdata)
        {
            if (msgdata.Length < 12)
                return;

            var msgtype = new string(msgdata.Take(8).Select(c => (char)c).ToArray());
            var msglen = BitConverter.ToInt32(msgdata, 8);

            if (msgdata.Length < msglen + 12 || msglen < 0)
                return;

            if (msgtype == "TREEDATA")
            {
                var reqdata = new byte[msglen];
                Array.Copy(msgdata, 12, reqdata, 0, msglen);
                reqdata = MyCompression.Decompress(reqdata);
                var envdata = MyAPIGateway.Utilities.SerializeFromBinary<EnvironmentData>(reqdata);

                MyAPIGateway.Parallel.Start(() => MessageHandler(envdata));
            }
        }

        private void MessageHandler(EnvironmentData envdata)
        {
            if (envdata.FromServer)
            {
                lock (((ICollection)ServerItems).SyncRoot)
                {
                    UpdateItems(ServerItems, envdata, "Server");
                }

                lock (((ICollection)ServerSectors).SyncRoot)
                {
                    UpdateSectors(ServerSectors, envdata, "Server");
                }

                lock (((ICollection)ServerItems2).SyncRoot)
                {
                    ServerItems2.Clear();

                    foreach (var item in ServerItems.Values)
                    {
                        ServerItems2[new MyTuple<long, long, int>(item.EntityId, item.LogicalSectorId, item.ItemNumber)] = item;
                    }
                }
            }
            else
            {
                Dictionary<MyTuple<long, long, int, int>, EnvironmentItem> items;
                Dictionary<MyTuple<long, long, int>, EnvironmentSector> sectors;
                var identityId = envdata.IdentityId;

                if (identityId != 0)
                {
                    lock (((ICollection)ClientItems).SyncRoot)
                    {
                        if (!ClientItems.TryGetValue(identityId, out items))
                        {
                            ClientItems[identityId] = items = new Dictionary<MyTuple<long, long, int, int>, EnvironmentItem>();
                        }
                    }

                    lock (((ICollection)ClientSectors).SyncRoot)
                    {
                        if (!ClientSectors.TryGetValue(identityId, out sectors))
                        {
                            ClientSectors[identityId] = sectors = new Dictionary<MyTuple<long, long, int>, EnvironmentSector>();
                        }
                    }

                    lock (((ICollection)items).SyncRoot)
                    {
                        UpdateItems(items, envdata, identityId.ToString());
                    }

                    lock (((ICollection)sectors).SyncRoot)
                    {
                        UpdateSectors(sectors, envdata, identityId.ToString());
                    }
                }
            }
        }

        private void MessageHandler(ushort handlerId, byte[] msgdata, ulong steamId, bool fromServer)
        {
            if (msgdata.Length < 12)
                return;

            var msgtype = new string(msgdata.Take(8).Select(c => (char)c).ToArray());
            var msglen = BitConverter.ToInt32(msgdata, 8);

            if (msgdata.Length < msglen + 12 || msglen < 0)
                return;

            if (msgtype == "TREEDATA")
            {
                var reqdata = new byte[msglen];
                Array.Copy(msgdata, 12, reqdata, 0, msglen);
                reqdata = MyCompression.Decompress(reqdata);
                var envdata = MyAPIGateway.Utilities.SerializeFromBinary<EnvironmentData>(reqdata);
                envdata.IdentityId = MyAPIGateway.Multiplayer.Players.TryGetIdentityId(steamId);
                envdata.FromServer = fromServer;

                MyAPIGateway.Parallel.Start(() => MessageHandler(envdata));
            }
        }
    }
}
