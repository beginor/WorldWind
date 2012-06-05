using System;
using System.Collections;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.XPath;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using WorldWind.Camera;
using WorldWind.Net;
using WorldWind.Net.Wms;
using WorldWind.Renderable;
using WorldWind.Terrain;
using WorldWind.Utilities;
using Font=System.Drawing.Font;
using Icon=WorldWind.Renderable.Icon;

namespace WorldWind.Configuration {
	/// <summary>
	/// Summary description for ConfigurationLoader.
	/// </summary>
	public class ConfigurationLoader {
		public static double ParseDouble(string s) {
			return double.Parse(s, CultureInfo.InvariantCulture);
		}

		public static World Load(string filename, Cache cache) {
			XPathDocument docNav = new XPathDocument(filename);
			XPathNavigator nav = docNav.CreateNavigator();

			XPathNodeIterator worldIter = nav.Select("/World[@Name]");
			if (worldIter.Count > 0) {
				worldIter.MoveNext();
				string worldName = worldIter.Current.GetAttribute("Name", "");
				double equatorialRadius = ParseDouble(worldIter.Current.GetAttribute("EquatorialRadius", ""));
				string layerDirectory = worldIter.Current.GetAttribute("LayerDirectory", "");

				if (layerDirectory.IndexOf(":") < 0) {
					layerDirectory = Path.Combine(Path.GetDirectoryName(filename), layerDirectory);
				}

				TerrainAccessor[] terrainAccessor = getTerrainAccessorsFromXPathNodeIterator(worldIter.Current.Select("TerrainAccessor"), Path.Combine(cache.CacheDirectory, worldName));

				World newWorld = new World(worldName, new Vector3(0, 0, 0), new Quaternion(0, 0, 0, 0), equatorialRadius, cache.CacheDirectory, (terrainAccessor != null ? terrainAccessor[0] : null) //TODO: Oops, World should be able to handle an array of terrainAccessors
					);

				newWorld.RenderableObjects = getRenderablesFromLayerDirectory(layerDirectory, newWorld, cache);

				return newWorld;
			}

			return null;
		}

		private static RenderableObjectList getRenderablesFromLayerDirectory(string layerDirectory, World parentWorld, Cache cache) {
			RenderableObjectList renderableCollection = new RenderableObjectList(parentWorld.Name);

			DirectoryInfo layerDir = new DirectoryInfo(layerDirectory);
			if (!layerDir.Exists) {
				return renderableCollection;
			}

			foreach (FileInfo layerFile in layerDir.GetFiles("*.xml")) {
				RenderableObjectList currentRenderable = getRenderableFromLayerFile(layerFile.FullName, parentWorld, cache);
				if (currentRenderable != null) {
					renderableCollection.Add(currentRenderable);
				}
			}

			return renderableCollection;
		}

		private static bool ParseBool(string booleanString) {
			if (booleanString == null
			    || booleanString.Trim().Length == 0) {
				return false;
			}

			booleanString = booleanString.Trim().ToLower();

			if (booleanString == "1") {
				return true;
			}
			else if (booleanString == "0") {
				return false;
			}
			else if (booleanString == "t") {
				return true;
			}
			else if (booleanString == "f") {
				return false;
			}
			else {
				return bool.Parse(booleanString);
			}
		}

		public static RenderableObjectList getRenderableFromLayerFile(string layerFile, World parentWorld, Cache cache) {
			return getRenderableFromLayerFile(layerFile, parentWorld, cache, true);
		}

		public static RenderableObjectList getRenderableFromLayerFile(string layerFile, World parentWorld, Cache cache, bool enableRefresh) {
			try {
				XPathDocument docNav = null;
				XPathNavigator nav = null;

				try {
					docNav = new XPathDocument(layerFile);
					nav = docNav.CreateNavigator();
				}
				catch (Exception ex) {
					Log.Write(ex);
					return null;
				}

				XPathNodeIterator iter = nav.Select("/LayerSet");

				if (iter.Count > 0) {
					iter.MoveNext();
					string redirect = iter.Current.GetAttribute("redirect", "");
					if (redirect != null
					    && redirect.Length > 0) {
						FileInfo layerFileInfo = new FileInfo(layerFile);

						try {
							Angle[] bbox = CameraBase.getViewBoundingBox();
							string viewBBox = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", bbox[0].ToString().TrimEnd('бу'), bbox[1].ToString().TrimEnd('бу'), bbox[2].ToString().TrimEnd('бу'), bbox[3].ToString().TrimEnd('бу'));

							//See if there is a ? already in the URL
							int flag = redirect.IndexOf("?");
							if (flag == -1) {
								redirect = redirect + "?BBOX=" + viewBBox;
							}
							else {
								redirect = redirect + "&BBOX=" + viewBBox;
							}

							WebDownload download = new WebDownload(redirect);

							string username = iter.Current.GetAttribute("username", "");
							string password = iter.Current.GetAttribute("password", "");

							if (username != null) {
								////	download.UserName = username;
								////	download.Password = password;
							}

							FileInfo tempDownloadFile = new FileInfo(layerFile.Replace(layerFileInfo.Extension, "_.tmp"));

							download.DownloadFile(tempDownloadFile.FullName, DownloadType.Unspecified);

							tempDownloadFile.Refresh();
							if (tempDownloadFile.Exists
							    && tempDownloadFile.Length > 0) {
								FileInfo tempStoreFile = new FileInfo(tempDownloadFile.FullName.Replace("_.tmp", ".tmp"));
								if (tempStoreFile.Exists) {
									tempStoreFile.Delete();
								}

								tempDownloadFile.MoveTo(tempStoreFile.FullName);
							}

							download.Dispose();

							using (StreamWriter writer = new StreamWriter(layerFile.Replace(layerFileInfo.Extension, ".uri"), false)) {
								writer.WriteLine(redirect);
							}
						}
						catch (Exception ex) {
							Log.Write(ex);
						}

						return getRenderableFromLayerFile(layerFile.Replace(layerFileInfo.Extension, ".tmp"), parentWorld, cache);
					}
					else {
						RenderableObjectList parentRenderable = null;

						string sourceUri = null;
						if (layerFile.EndsWith(".tmp")) {
							//get source url
							using (StreamReader reader = new StreamReader(layerFile.Replace(".tmp", ".uri"))) {
								sourceUri = reader.ReadLine();
							}
						}
						string refreshString = iter.Current.GetAttribute("Refresh", "");
						if (refreshString != null
						    && refreshString.Length > 0) {
							if (iter.Current.Select("Icon").Count > 0) {
								parentRenderable = new Icons(iter.Current.GetAttribute("Name", ""), (sourceUri != null ? sourceUri : layerFile), TimeSpan.FromSeconds(ParseDouble(refreshString)), parentWorld, cache);
							}
								#region by zzm
							else if (iter.Current.Select("MyIcon").Count > 0) {
								// add my icon here.
								parentRenderable = new MyIcons(iter.Current.GetAttribute("Name", ""), (sourceUri != null ? sourceUri : layerFile), TimeSpan.FromSeconds(ParseDouble(refreshString)), parentWorld, cache);
							}
							else if (iter.Current.Select("MyChart").Count > 0) {
								// add chart here.
								parentRenderable = new MyCharts(iter.Current.GetAttribute("Name", ""), (sourceUri != null ? sourceUri : layerFile), TimeSpan.FromSeconds(ParseDouble(refreshString)), parentWorld, cache);
							}
								#endregion

							else {
								parentRenderable = new RenderableObjectList(iter.Current.GetAttribute("Name", ""), (sourceUri != null ? sourceUri : layerFile), TimeSpan.FromSeconds(ParseDouble(refreshString)), parentWorld, cache);
							}
						}
						else {
							if (iter.Current.Select("Icon").Count > 0) {
								parentRenderable = new Icons(iter.Current.GetAttribute("Name", ""));
							}
								#region by zzm
							else if (iter.Current.Select("MyIcon").Count > 0) {
								// add my icon here.
								parentRenderable = new MyIcons(iter.Current.GetAttribute("Name", ""));
							}
							else if (iter.Current.Select("MyChart").Count > 0) {
								// add chart here.
								parentRenderable = new MyCharts(iter.Current.GetAttribute("Name", ""));
							}
								#endregion

							else {
								parentRenderable = new RenderableObjectList(iter.Current.GetAttribute("Name", ""));
							}
						}

						parentRenderable.ParentList = parentWorld.RenderableObjects;

						if (World.Settings.useDefaultLayerStates) {
							parentRenderable.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
						}
						else {
							parentRenderable.IsOn = IsLayerOn(parentRenderable);
						}

						parentRenderable.ShowOnlyOneLayer = ParseBool(iter.Current.GetAttribute("ShowOnlyOneLayer", ""));

						parentRenderable.MetaData.Add("XmlSource", (sourceUri != null ? sourceUri : layerFile));

						parentRenderable.MetaData.Add("World", parentWorld);
						parentRenderable.MetaData.Add("Cache", cache);
						parentRenderable.ParentList = parentWorld.RenderableObjects;

						string renderPriorityString = iter.Current.GetAttribute("RenderPriority", "");
						if (renderPriorityString != null) {
							if (String.Compare(renderPriorityString, "Icons", false) == 0) {
								parentRenderable.RenderPriority = RenderPriority.Icons;
							}
							else if (String.Compare(renderPriorityString, "LinePaths", false) == 0) {
								parentRenderable.RenderPriority = RenderPriority.LinePaths;
							}
							else if (String.Compare(renderPriorityString, "Placenames", false) == 0) {
								parentRenderable.RenderPriority = RenderPriority.Placenames;
							}
							else if (String.Compare(renderPriorityString, "AtmosphericImages", false) == 0) {
								parentRenderable.RenderPriority = RenderPriority.AtmosphericImages;
							}
						}

						string infoUri = iter.Current.GetAttribute("InfoUri", "");

						if (infoUri != null
						    && infoUri.Length > 0) {
							if (parentRenderable.MetaData.Contains("InfoUri")) {
								parentRenderable.MetaData["InfoUri"] = infoUri;
							}
							else {
								parentRenderable.MetaData.Add("InfoUri", infoUri);
							}
						}

						addImageLayersFromXPathNodeIterator(iter.Current.Select("ImageLayer"), parentWorld, parentRenderable);
						addQuadTileLayersFromXPathNodeIterator(iter.Current.Select("QuadTileSet"), parentWorld, parentRenderable, cache);
						addPathList(iter.Current.Select("PathList"), parentWorld, parentRenderable);
						addPolygonFeature(iter.Current.Select("PolygonFeature"), parentWorld, parentRenderable);
						addLineFeature(iter.Current.Select("LineFeature"), parentWorld, parentRenderable);
						addTiledPlacenameSet(iter.Current.Select("TiledPlacenameSet"), parentWorld, parentRenderable);
						addIcon(iter.Current.Select("Icon"), parentWorld, parentRenderable, cache);
						addScreenOverlays(iter.Current.Select("ScreenOverlay"), parentWorld, parentRenderable, cache);
						addChildLayerSet(iter.Current.Select("ChildLayerSet"), parentWorld, parentRenderable, cache);

						#region by zzm
						AddMyIcon(iter.Current.Select("MyIcon"), parentWorld, parentRenderable, cache);
						AddMyChart(iter.Current.Select("MyChart"), parentWorld, parentRenderable, cache);
						AddMesh(iter.Current.Select("MeshLayer"), parentWorld, parentRenderable, cache);
						#endregion

						addExtendedInformation(iter.Current.Select("ExtendedInformation"), parentRenderable);

						if (parentRenderable.RefreshTimer != null && enableRefresh) {
							parentRenderable.RefreshTimer.Start();
						}
						return parentRenderable;
					}
				}
			}
			catch (Exception ex) {
				Log.Write(ex);
				//Utility.Log.Write(layerFile);
			}
			return null;
		}

		public static bool IsLayerOn(RenderableObject ro) {
			string path = getRenderablePathString(ro);
			foreach (string s in World.Settings.loadedLayers) {
				if (s.Equals(path)) {
					return true;
				}
			}

			return false;
		}

		private static RenderableObjectList addChildLayerSet(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string layerName = iter.Current.GetAttribute("Name", "");
					bool showAtStartup = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					bool showOnlyOneLayer = ParseBool(iter.Current.GetAttribute("ShowOnlyOneLayer", ""));

					string redirect = iter.Current.GetAttribute("redirect", "");

					if (redirect != null
					    && redirect.Length > 0) {
						return getRenderableFromLayerFile(redirect, parentWorld, cache);
					}

					RenderableObjectList rol;
					if (iter.Current.Select("Icon").Count > 0) {
						rol = new Icons(layerName);
					}
						#region by zzm
					else if (iter.Current.Select("MyIcon").Count > 0) {
						rol = new MyIcons(layerName);
					}
					else if (iter.Current.Select("MyChart").Count > 0) {
						rol = new MyCharts(layerName);
					}
					else {
						rol = new RenderableObjectList(layerName);
					}
					#endregion

					rol.ParentList = parentRenderable;

					if (World.Settings.useDefaultLayerStates) {
						rol.IsOn = showAtStartup;
					}
					else {
						rol.IsOn = IsLayerOn(rol);
					}
					rol.ShowOnlyOneLayer = showOnlyOneLayer;
					rol.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);

					string renderPriorityString = iter.Current.GetAttribute("RenderPriority", "");
					if (renderPriorityString != null) {
						if (String.Compare(renderPriorityString, "Icons", false) == 0) {
							rol.RenderPriority = RenderPriority.Icons;
						}
						else if (String.Compare(renderPriorityString, "LinePaths", false) == 0) {
							rol.RenderPriority = RenderPriority.LinePaths;
						}
						else if (String.Compare(renderPriorityString, "Placenames", false) == 0) {
							rol.RenderPriority = RenderPriority.Placenames;
						}
						else if (String.Compare(renderPriorityString, "AtmosphericImages", false) == 0) {
							rol.RenderPriority = RenderPriority.AtmosphericImages;
						}
					}

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (rol.MetaData.Contains("InfoUri")) {
							rol.MetaData["InfoUri"] = infoUri;
						}
						else {
							rol.MetaData.Add("InfoUri", infoUri);
						}
					}

					addImageLayersFromXPathNodeIterator(iter.Current.Select("ImageLayer"), parentWorld, rol);
					addQuadTileLayersFromXPathNodeIterator(iter.Current.Select("QuadTileSet"), parentWorld, rol, cache);
					addPolygonFeature(iter.Current.Select("PolygonFeature"), parentWorld, rol);
					addLineFeature(iter.Current.Select("LineFeature"), parentWorld, rol);
					addPathList(iter.Current.Select("PathList"), parentWorld, rol);
					addTiledPlacenameSet(iter.Current.Select("TiledPlacenameSet"), parentWorld, rol);
					addIcon(iter.Current.Select("Icon"), parentWorld, rol, cache);
					addScreenOverlays(iter.Current.Select("ScreenOverlay"), parentWorld, rol, cache);
					addChildLayerSet(iter.Current.Select("ChildLayerSet"), parentWorld, rol, cache);

					#region by zzm
					AddMyIcon(iter.Current.Select("MyIcon"), parentWorld, rol, cache);
					AddMyChart(iter.Current.Select("MyChart"), parentWorld, rol, cache);
					AddMesh(iter.Current.Select("MeshLayer"), parentWorld, rol, cache);
					#endregion

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), rol);
					parentRenderable.Add(rol);
				}
			}

			return null;
		}

		private static ImageTileService getImageTileServiceFromXPathNodeIterator(XPathNodeIterator iter) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string serverUrl = getInnerTextFromFirstChild(iter.Current.Select("ServerUrl"));
					string dataSetName = getInnerTextFromFirstChild(iter.Current.Select("DataSetName"));
					string serverLogoFilePath = getInnerTextFromFirstChild(iter.Current.Select("ServerLogoFilePath"));

					TimeSpan cacheExpiration = getCacheExpiration(iter.Current.Select("CacheExpirationTime"));

					if (serverLogoFilePath != null && serverLogoFilePath.Length > 0
					    && !Path.IsPathRooted(serverLogoFilePath)) {
						serverLogoFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), serverLogoFilePath);
					}

					if (serverUrl != null
					    && dataSetName != null) {
						return new ImageTileService(dataSetName, serverUrl, serverLogoFilePath, cacheExpiration);
					}
				}
			}

			return null;
		}

		private static void addQuadTileLayersFromXPathNodeIterator(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					bool showAtStartup = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));

					double north = 0;
					double south = 0;
					double west = 0;
					double east = 0;

					XPathNodeIterator boundingBoxIter = iter.Current.Select("BoundingBox");
					if (boundingBoxIter.Count > 0) {
						boundingBoxIter.MoveNext();
						north = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("North")));
						south = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("South")));
						west = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("West")));
						east = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("East")));
					}

					string terrainMappedString = getInnerTextFromFirstChild(iter.Current.Select("TerrainMapped"));
					string renderStrutsString = getInnerTextFromFirstChild(iter.Current.Select("RenderStruts"));

					bool terrainMapped = true;

					if (terrainMappedString != null) {
						terrainMapped = ParseBool(terrainMappedString);
					}
					XPathNodeIterator imageAccessorIter = iter.Current.Select("ImageAccessor");

					QuadTileSet qts = null;

					byte opacity = 255;

					if (imageAccessorIter.Count > 0) {
						imageAccessorIter.MoveNext();
						double levelZeroTileSizeDegrees = ParseDouble(getInnerTextFromFirstChild(imageAccessorIter.Current.Select("LevelZeroTileSizeDegrees")));
						int numberLevels = Int32.Parse(getInnerTextFromFirstChild(imageAccessorIter.Current.Select("NumberLevels")));
						int textureSizePixels = Int32.Parse(getInnerTextFromFirstChild(imageAccessorIter.Current.Select("TextureSizePixels")));
						string imageFileExtension = getInnerTextFromFirstChild(imageAccessorIter.Current.Select("ImageFileExtension"));
						string permanentDirectory = getInnerTextFromFirstChild(imageAccessorIter.Current.Select("PermanentDirectory"));
						if (permanentDirectory == null
						    || permanentDirectory.Length == 0) {
							permanentDirectory = getInnerTextFromFirstChild(imageAccessorIter.Current.Select("PermanantDirectory"));
						}

						TimeSpan dataExpiration = getCacheExpiration(imageAccessorIter.Current.Select("DataExpirationTime"));

						string duplicateTilePath = getInnerTextFromFirstChild(imageAccessorIter.Current.Select("DuplicateTilePath"));
						string cacheDir = getInnerTextFromFirstChild(imageAccessorIter.Current.Select("CacheDirectory"));

						if (cacheDir == null
						    || cacheDir.Length == 0) {
							cacheDir = String.Format("{0}{1}{2}{1}{3}", cache.CacheDirectory, Path.DirectorySeparatorChar, getRenderablePathString(parentRenderable), name);
						}
						else {
							cacheDir = Path.Combine(cache.CacheDirectory, cacheDir);
						}

						if (permanentDirectory != null
						    && permanentDirectory.IndexOf(":") < 0) {
							permanentDirectory = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), permanentDirectory);
						}

						if (duplicateTilePath != null
						    && duplicateTilePath.IndexOf(":") < 0) {
							duplicateTilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), duplicateTilePath);
						}

						if (permanentDirectory != null) {
							ImageStore ia = new ImageStore();
							ia.DataDirectory = permanentDirectory;
							ia.LevelZeroTileSizeDegrees = levelZeroTileSizeDegrees;
							ia.LevelCount = numberLevels;
							ia.ImageExtension = imageFileExtension;
							//doesn't work when this is set
							//ia.CacheDirectory = cacheDir;

							if (duplicateTilePath != null
							    && duplicateTilePath.Length > 0) {
								ia.DuplicateTexturePath = duplicateTilePath;
							}

							//	ia.DataExpirationTime = dataExpiration;

							qts = new QuadTileSet(name, parentWorld, distanceAboveSurface, north, south, west, east, terrainMapped, ia);
						}
						else {
							XPathNodeIterator imageTileServiceIter = imageAccessorIter.Current.Select("ImageTileService");
							if (imageTileServiceIter.Count > 0) {
								imageTileServiceIter.MoveNext();

								string serverUrl = getInnerTextFromFirstChild(imageTileServiceIter.Current.Select("ServerUrl"));
								string dataSetName = getInnerTextFromFirstChild(imageTileServiceIter.Current.Select("DataSetName"));
								string serverLogoFilePath = getInnerTextFromFirstChild(imageTileServiceIter.Current.Select("ServerLogoFilePath"));

								TimeSpan cacheExpiration = getCacheExpiration(imageTileServiceIter.Current.Select("CacheExpirationTime"));

								if (serverLogoFilePath != null && serverLogoFilePath.Length > 0
								    && !Path.IsPathRooted(serverLogoFilePath)) {
									serverLogoFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), serverLogoFilePath);
								}

								string opacityString = getInnerTextFromFirstChild(imageTileServiceIter.Current.Select("Opacity"));

								if (opacityString != null) {
									opacity = byte.Parse(opacityString);
								}

								ImageStore ia = new NltImageStore(dataSetName, serverUrl);
								ia.DataDirectory = null;
								ia.LevelZeroTileSizeDegrees = levelZeroTileSizeDegrees;
								ia.LevelCount = numberLevels;
								ia.ImageExtension = imageFileExtension;
								ia.CacheDirectory = cacheDir;

								//if(newImageTileService.CacheExpirationTime == TimeSpan.MaxValue)
								//	{
								//	ia.DataExpirationTime = dataExpiration;
								//	}
								//	else
								//	{
								//	ia.DataExpirationTime = newImageTileService.CacheExpirationTime;
								//	}

								qts = new QuadTileSet(name, parentWorld, distanceAboveSurface, north, south, west, east, terrainMapped, ia);

								qts.Opacity = opacity;
								if (serverLogoFilePath != null) {
									qts.ServerLogoFilePath = serverLogoFilePath;
								}
							}
							else {
								XPathNodeIterator wmsAccessorIter = imageAccessorIter.Current.Select("WMSAccessor");
								if (wmsAccessorIter.Count > 0) {
									wmsAccessorIter.MoveNext();

									WmsImageStore wmsLayerStore = new WmsImageStore();

									wmsLayerStore.ImageFormat = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("ImageFormat"));

									wmsLayerStore.ImageExtension = imageFileExtension;
									wmsLayerStore.CacheDirectory = cacheDir;
									//wmsLayerAccessor.IsTransparent = ParseBool(getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("UseTransparency")));
									wmsLayerStore.ServerGetMapUrl = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("ServerGetMapUrl"));
									wmsLayerStore.Version = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("Version"));
									wmsLayerStore.WMSLayerName = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("WMSLayerName"));

									string username = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("Username"));
									string password = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("Password"));
									string wmsStyleName = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("WMSLayerStyle"));
									string serverLogoPath = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("ServerLogoFilePath"));
									string opacityString = getInnerTextFromFirstChild(wmsAccessorIter.Current.Select("Opacity"));

									if (serverLogoPath != null && serverLogoPath.Length > 0
									    && !Path.IsPathRooted(serverLogoPath)) {
										serverLogoPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), serverLogoPath);
									}
									if (opacityString != null) {
										opacity = byte.Parse(opacityString);
									}

									TimeSpan cacheExpiration = getCacheExpiration(iter.Current.Select("CacheExpirationTime"));

									//	if(username != null && username.Length > 0)
									//		wmsLayerStore.Username = username;

									//	if(password != null)
									//		wmsLayerAccessor.Password = password;

									if (wmsStyleName != null
									    && wmsStyleName.Length > 0) {
										wmsLayerStore.WMSLayerStyle = wmsStyleName;
									}
									else {
										wmsLayerStore.WMSLayerStyle = "";
									}

									wmsLayerStore.LevelCount = numberLevels;
									wmsLayerStore.LevelZeroTileSizeDegrees = levelZeroTileSizeDegrees;

									//	if(cacheExpiration == TimeSpan.MaxValue)
									//		imageAccessor.DataExpirationTime = dataExpiration;
									//	else
									//		imageAccessor.DataExpirationTime = cacheExpiration;

									if (wmsLayerStore != null) {
										qts = new QuadTileSet(name, parentWorld, distanceAboveSurface, north, south, west, east, terrainMapped, wmsLayerStore);

										qts.Opacity = opacity;
										if (serverLogoPath != null) {
											qts.ServerLogoFilePath = serverLogoPath;
										}
									}
								}
							}
						}

						if (qts != null) {
							string infoUri = iter.Current.GetAttribute("InfoUri", "");

							if (infoUri != null
							    && infoUri.Length > 0) {
								if (qts.MetaData.Contains("InfoUri")) {
									qts.MetaData["InfoUri"] = infoUri;
								}
								else {
									qts.MetaData.Add("InfoUri", infoUri);
								}
							}

							if (iter.Current.Select("TransparentColor").Count > 0) {
								Color c = getColor(iter.Current.Select("TransparentColor"));
								qts.ColorKey = c.ToArgb();
							}

							if (iter.Current.Select("TransparentMinValue").Count > 0) {
								qts.ColorKey = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("TransparentMinValue")));
							}

							if (iter.Current.Select("TransparentMaxValue").Count > 0) {
								qts.ColorKeyMax = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("TransparentMaxValue")));
							}

							if (renderStrutsString != null) {
								qts.RenderStruts = ParseBool(renderStrutsString);
							}

							qts.ParentList = parentRenderable;
							if (World.Settings.useDefaultLayerStates) {
								qts.IsOn = showAtStartup;
							}
							else {
								qts.IsOn = IsLayerOn(qts);
							}

							qts.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
							addExtendedInformation(iter.Current.Select("ExtendedInformation"), qts);
							parentRenderable.Add(qts);
						}
					}
				}
			}
		}

		private static TimeSpan getCacheExpiration(XPathNodeIterator iter) {
			TimeSpan ts = TimeSpan.MaxValue;

			if (iter.Count > 0) {
				iter.MoveNext();

				string daysString = getInnerTextFromFirstChild(iter.Current.Select("Days"));
				string hoursString = getInnerTextFromFirstChild(iter.Current.Select("Hours"));
				string minutesString = getInnerTextFromFirstChild(iter.Current.Select("Mins"));
				string secondsString = getInnerTextFromFirstChild(iter.Current.Select("Seconds"));

				double days = 0;
				double hours = 0;
				double minutes = 0;
				double seconds = 0;

				if (daysString != null) {
					days = ParseDouble(daysString);
				}

				if (hoursString != null) {
					hours = ParseDouble(hoursString);
				}

				if (minutesString != null) {
					minutes = ParseDouble(minutesString);
				}

				if (secondsString != null) {
					seconds = ParseDouble(secondsString);
				}

				ts = new TimeSpan((int) days, (int) hours, (int) minutes, (int) seconds);
			}

			return ts;
		}

		private static void addExtendedInformation(XPathNodeIterator iter, RenderableObject renderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string toolBarImage = getInnerTextFromFirstChild(iter.Current.Select("ToolBarImage"));

					if (toolBarImage != null) {
						if (toolBarImage.Length > 0
						    && !Path.IsPathRooted(toolBarImage)) {
							Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), toolBarImage);
						}

						renderable.MetaData.Add("ToolBarImagePath", toolBarImage);
					}
				}
			}
		}

		public static string GetRenderablePathString(RenderableObject renderable) {
			return getRenderablePathString(renderable);
		}

		private static string getRenderablePathString(RenderableObject renderable) {
			if (renderable.ParentList == null) {
				return renderable.Name;
			}
			else {
				return getRenderablePathString(renderable.ParentList) + Path.DirectorySeparatorChar + renderable.Name;
			}
		}

		private static void addImageLayersFromXPathNodeIterator(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					string texturePath = getInnerTextFromFirstChild(iter.Current.Select("TexturePath"));
					byte opacity = byte.Parse(getInnerTextFromFirstChild(iter.Current.Select("Opacity")));

					TimeSpan dataExpiration = getCacheExpiration(iter.Current.Select("CacheExpirationTime"));
					string imageUrl = null;

					if (texturePath.StartsWith("http://")) {
						imageUrl = texturePath;
						texturePath = null;
					}

					XPathNodeIterator boundingBoxIter = iter.Current.Select("BoundingBox");
					if (boundingBoxIter.Count > 0) {
						boundingBoxIter.MoveNext();

						double north = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("North")));
						double south = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("South")));
						double west = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("West")));
						double east = ParseDouble(getInnerTextFromFirstChild(boundingBoxIter.Current.Select("East")));

						ImageLayer im = new ImageLayer(name, parentWorld, distanceAboveSurface, texturePath, south, north, west, east, opacity, parentWorld.TerrainAccessor);

						im.ImageUrl = imageUrl;
						im.CacheExpiration = dataExpiration;

						addExtendedInformation(iter.Current.Select("ExtendedInformation"), im);

						string infoUri = iter.Current.GetAttribute("InfoUri", "");

						if (infoUri != null
						    && infoUri.Length > 0) {
							if (im.MetaData.Contains("InfoUri")) {
								im.MetaData["InfoUri"] = infoUri;
							}
							else {
								im.MetaData.Add("InfoUri", infoUri);
							}
						}

						im.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);

						im.ParentList = parentRenderable;
						if (World.Settings.useDefaultLayerStates) {
							im.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
						}
						else {
							im.IsOn = IsLayerOn(im);
						}
						parentRenderable.ChildObjects.Add(im);
					}
				}
			}
		}

		private static void addScreenOverlays(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					string imageUri = getInnerTextFromFirstChild(iter.Current.Select("ImageUri"));
					string startXString = getInnerTextFromFirstChild(iter.Current.Select("StartX"));
					string startYString = getInnerTextFromFirstChild(iter.Current.Select("StartY"));
					string widthString = getInnerTextFromFirstChild(iter.Current.Select("Width"));
					string heightString = getInnerTextFromFirstChild(iter.Current.Select("Height"));
					string opacityString = getInnerTextFromFirstChild(iter.Current.Select("Opacity"));
					string showHeaderString = getInnerTextFromFirstChild(iter.Current.Select("ShowHeader"));
					string alignmentString = getInnerTextFromFirstChild(iter.Current.Select("Alignment"));
					string clickableUrl = getInnerTextFromFirstChild(iter.Current.Select("ClickableUrl"));
					string refreshTimeString = iter.Current.GetAttribute("Refresh", "");
					string hideBorderString = getInnerTextFromFirstChild(iter.Current.Select("HideBorder"));

					if (startXString != null
					    && startYString != null) {
						int startX = int.Parse(startXString);
						int startY = int.Parse(startYString);

						ScreenOverlay overlay = new ScreenOverlay(name, startX, startY, imageUri);

						if (widthString != null) {
							overlay.Width = int.Parse(widthString);
						}
						if (heightString != null) {
							overlay.Height = int.Parse(heightString);
						}

						if (alignmentString != null) {
							if (alignmentString.ToLower().Equals("left")) {
								overlay.Alignment = ScreenAlignment.Left;
							}
							else if (alignmentString.ToLower().Equals("right")) {
								overlay.Alignment = ScreenAlignment.Right;
							}
						}

						if (clickableUrl != null
						    && clickableUrl.Length > 0) {
							overlay.ClickableUrl = clickableUrl;
						}

						if (hideBorderString != null
						    && hideBorderString.Length > 0) {
							overlay.HideBorder = ParseBool(hideBorderString);
						}

						if (iter.Current.Select("BorderColor").Count != 0) {
							overlay.BorderColor = getColor(iter.Current.Select("BorderColor"));
						}

						string cachePath = String.Format("{0}{1}{2}{1}{3}{1}{3}", cache.CacheDirectory, Path.DirectorySeparatorChar, getRenderablePathString(parentRenderable), name);

						if (refreshTimeString != null
						    && refreshTimeString.Length > 0) {
							overlay.RefreshTimeSec = ParseDouble(refreshTimeString);
						}

						overlay.SaveFilePath = cachePath;
						addExtendedInformation(iter.Current.Select("ExtendedInformation"), overlay);

						string infoUri = iter.Current.GetAttribute("InfoUri", "");

						if (infoUri != null
						    && infoUri.Length > 0) {
							if (overlay.MetaData.Contains("InfoUri")) {
								overlay.MetaData["InfoUri"] = infoUri;
							}
							else {
								overlay.MetaData.Add("InfoUri", infoUri);
							}
						}

						overlay.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
						overlay.ParentList = parentRenderable;
						if (opacityString != null) {
							overlay.Opacity = byte.Parse(opacityString);
						}

						if (showHeaderString != null) {
							overlay.ShowHeader = ParseBool(showHeaderString);
						}

						if (World.Settings.useDefaultLayerStates) {
							overlay.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
						}
						else {
							overlay.IsOn = IsLayerOn(overlay);
						}

						parentRenderable.ChildObjects.Add(overlay);
					}
				}
			}
		}

		private static void addScreenOverlaysToIcon(XPathNodeIterator iter, World parentWorld, Icon icon, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					string imageUri = getInnerTextFromFirstChild(iter.Current.Select("ImageUri"));
					string startXString = getInnerTextFromFirstChild(iter.Current.Select("StartX"));
					string startYString = getInnerTextFromFirstChild(iter.Current.Select("StartY"));
					string widthString = getInnerTextFromFirstChild(iter.Current.Select("Width"));
					string heightString = getInnerTextFromFirstChild(iter.Current.Select("Height"));
					string opacityString = getInnerTextFromFirstChild(iter.Current.Select("Opacity"));
					string showHeaderString = getInnerTextFromFirstChild(iter.Current.Select("ShowHeader"));
					string refreshTimeString = iter.Current.GetAttribute("Refresh", "");
					string alignmentString = getInnerTextFromFirstChild(iter.Current.Select("Alignment"));
					string clickableUrl = getInnerTextFromFirstChild(iter.Current.Select("ClickableUrl"));
					string hideBorderString = getInnerTextFromFirstChild(iter.Current.Select("HideBorder"));

					if (startXString != null
					    && startYString != null) {
						int startX = int.Parse(startXString);
						int startY = int.Parse(startYString);

						ScreenOverlay overlay = new ScreenOverlay(name, startX, startY, imageUri);

						if (widthString != null) {
							overlay.Width = int.Parse(widthString);
						}
						if (heightString != null) {
							overlay.Height = int.Parse(heightString);
						}

						if (alignmentString != null) {
							if (alignmentString.ToLower().Equals("left")) {
								overlay.Alignment = ScreenAlignment.Left;
							}
							else if (alignmentString.ToLower().Equals("right")) {
								overlay.Alignment = ScreenAlignment.Right;
							}
						}

						if (clickableUrl != null
						    && clickableUrl.Length > 0) {
							overlay.ClickableUrl = clickableUrl;
						}

						if (hideBorderString != null
						    && hideBorderString.Length > 0) {
							overlay.HideBorder = ParseBool(hideBorderString);
						}

						string cachePath = String.Format("{0}{1}{2}{1}{3}{1}{3}", cache.CacheDirectory, Path.DirectorySeparatorChar, getRenderablePathString(icon), name);

						if (refreshTimeString != null
						    && refreshTimeString.Length > 0) {
							overlay.RefreshTimeSec = ParseDouble(refreshTimeString);
						}

						overlay.SaveFilePath = cachePath;
						addExtendedInformation(iter.Current.Select("ExtendedInformation"), overlay);

						string infoUri = iter.Current.GetAttribute("InfoUri", "");

						if (infoUri != null
						    && infoUri.Length > 0) {
							if (overlay.MetaData.Contains("InfoUri")) {
								overlay.MetaData["InfoUri"] = infoUri;
							}
							else {
								overlay.MetaData.Add("InfoUri", infoUri);
							}
						}

						overlay.MetaData.Add("XmlSource", (string) icon.MetaData["XmlSource"]);
						overlay.ParentList = icon.ParentList;
						if (opacityString != null) {
							overlay.Opacity = byte.Parse(opacityString);
						}

						if (showHeaderString != null) {
							overlay.ShowHeader = ParseBool(showHeaderString);
						}

						if (World.Settings.useDefaultLayerStates) {
							overlay.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
						}
						else {
							overlay.IsOn = IsLayerOn(overlay);
						}

						icon.AddOverlay(overlay);
					}
				}
			}
		}

		private static Color getColor(XPathNodeIterator iter) {
			iter.MoveNext();
			byte r = 0;
			byte g = 0;
			byte b = 0;
			byte a = 255;

			string redString = getInnerTextFromFirstChild(iter.Current.Select("Red"));
			string greenString = getInnerTextFromFirstChild(iter.Current.Select("Green"));
			string blueString = getInnerTextFromFirstChild(iter.Current.Select("Blue"));
			string alphaString = getInnerTextFromFirstChild(iter.Current.Select("Alpha"));

			r = byte.Parse(redString);
			g = byte.Parse(greenString);
			b = byte.Parse(blueString);
			if (alphaString != null) {
				a = byte.Parse(alphaString);
			}

			return Color.FromArgb(a, r, g, b);
		}

		private static FontDescription getDisplayFont(XPathNodeIterator iter) {
			FontDescription fd = new FontDescription();

			if (iter.MoveNext()) {
				fd.FaceName = getInnerTextFromFirstChild(iter.Current.Select("Family"));
				fd.Height = (int) (float.Parse(getInnerTextFromFirstChild(iter.Current.Select("Size")))*1.5f);

				XPathNodeIterator styleIter = iter.Current.Select("Style");
				if (styleIter.Count > 0) {
					styleIter.MoveNext();

					string isBoldString = getInnerTextFromFirstChild(styleIter.Current.Select("IsBold"));
					string isItalicString = getInnerTextFromFirstChild(styleIter.Current.Select("IsItalic"));

					if (isBoldString != null) {
						bool isBold = ParseBool(isBoldString);
						if (isBold) {
							fd.Weight = FontWeight.Bold;
						}
					}

					if (isItalicString != null) {
						bool isItalic = ParseBool(isItalicString);
						if (isItalic) {
							fd.IsItalic = isItalic;
						}
					}
				}
				else {
					fd.Weight = FontWeight.Regular;
				}
			}

			return fd;
		}

		private static void addTiledPlacenameSet(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					double minimumDisplayAltitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("MinimumDisplayAltitude")));
					double maximumDisplayAltitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("MaximumDisplayAltitude")));

					string placenameListFilePath = getInnerTextFromFirstChild(iter.Current.Select("PlacenameListFilePath"));

					if (!Path.IsPathRooted(placenameListFilePath)) {
						Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), placenameListFilePath);
					}
					string winColorName = getInnerTextFromFirstChild(iter.Current.Select("WinColorName"));
					string iconFilePath = getInnerTextFromFirstChild(iter.Current.Select("IconFilePath"));

					FontDescription fd = getDisplayFont(iter.Current.Select("DisplayFont"));

					Color c = Color.White;

					if (winColorName != null) {
						c = Color.FromName(winColorName);
					}
					else {
						c = getColor(iter.Current.Select("RGBColor"));
					}

					TiledPlacenameSet tps = new TiledPlacenameSet(name, parentWorld, distanceAboveSurface, maximumDisplayAltitude, minimumDisplayAltitude, placenameListFilePath, fd, c, iconFilePath);

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), tps);

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (tps.MetaData.Contains("InfoUri")) {
							tps.MetaData["InfoUri"] = infoUri;
						}
						else {
							tps.MetaData.Add("InfoUri", infoUri);
						}
					}

					tps.MetaData.Add("PlacenameDataFile", placenameListFilePath);
					tps.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
					tps.ParentList = parentRenderable;

					if (World.Settings.useDefaultLayerStates) {
						tps.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						tps.IsOn = IsLayerOn(tps);
					}
					parentRenderable.ChildObjects.Add(tps);

					parentRenderable.RenderPriority = RenderPriority.Placenames;
				}
			}
		}

		private static void addIcon(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					double latitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("Latitude/Value")));
					double longitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("Longitude/Value")));

					string refreshString = iter.Current.GetAttribute("Refresh", "");
					string description = getInnerTextFromFirstChild(iter.Current.Select("Description"));

					string isRotatedString = getInnerTextFromFirstChild(iter.Current.Select("IsRotated"));
					string rotationDegreesString = getInnerTextFromFirstChild(iter.Current.Select("RotationDegrees"));

					string minimumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MinimumDisplayAltitude"));
					string maximumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MaximumDisplayAltitude"));

					string clickableUrl = getInnerTextFromFirstChild(iter.Current.Select("ClickableUrl"));

					string textureFilePath = getInnerTextFromFirstChild(iter.Current.Select("TextureFilePath"));

					if (textureFilePath.Length > 0 && !Path.IsPathRooted(textureFilePath)
					    && !textureFilePath.ToLower().StartsWith("http://")) {
						// Use absolute path to icon image
						textureFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), textureFilePath);
					}

					if (!Path.IsPathRooted(textureFilePath)) {
						Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), textureFilePath);
					}

					Icon ic = new Icon(name, latitude, longitude, distanceAboveSurface);

					ic.TextureFileName = textureFilePath;
					ic.Width = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("IconWidthPixels")));
					ic.Height = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("IconHeightPixels")));

					if (refreshString != null
					    && refreshString.Length > 0) {
						ic.RefreshInterval = TimeSpan.FromSeconds(ParseDouble(refreshString));
					}

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (ic.MetaData.Contains("InfoUri")) {
							ic.MetaData["InfoUri"] = infoUri;
						}
						else {
							ic.MetaData.Add("InfoUri", infoUri);
						}
					}

					if (description != null) {
						ic.Description = description;
					}

					if (clickableUrl != null) {
						ic.ClickableActionURL = clickableUrl;
					}

					if (maximumDisplayAltitudeString != null) {
						ic.MaximumDisplayDistance = ParseDouble(maximumDisplayAltitudeString);
					}

					if (minimumDisplayAltitudeString != null) {
						ic.MinimumDisplayDistance = ParseDouble(minimumDisplayAltitudeString);
					}

					string onClickZoomAltString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomAltitude"));
					string onClickZoomHeadingString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomHeading"));
					string onClickZoomTiltString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomTilt"));

					if (onClickZoomAltString != null) {
						ic.OnClickZoomAltitude = ParseDouble(onClickZoomAltString);
					}

					if (onClickZoomHeadingString != null) {
						ic.OnClickZoomHeading = ParseDouble(onClickZoomHeadingString);
					}

					if (onClickZoomTiltString != null) {
						ic.OnClickZoomTilt = ParseDouble(onClickZoomTiltString);
					}

					if (isRotatedString != null) {
						ic.IsRotated = ParseBool(isRotatedString);
					}

					if (rotationDegreesString != null) {
						if (!ic.IsRotated) {
							ic.IsRotated = true;
						}

						ic.Rotation = Angle.FromDegrees(ParseDouble(rotationDegreesString));
					}

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), ic);

					ic.ParentList = parentRenderable;

					string cachePath = String.Format("{0}{1}{2}{1}{3}{1}{3}", cache.CacheDirectory, Path.DirectorySeparatorChar, getRenderablePathString(parentRenderable), name);

					ic.SaveFilePath = cachePath;

					if (World.Settings.useDefaultLayerStates) {
						ic.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						ic.IsOn = IsLayerOn(ic);
					}
					ic.MetaData["XmlSource"] = (string) parentRenderable.MetaData["XmlSource"];

					Icons parentIconList = (Icons) parentRenderable;

					parentIconList.Add(ic);

					addScreenOverlaysToIcon(iter.Current.Select("ScreenOverlay"), parentWorld, ic, cache);
				}
			}
		}

		private static void addPathList(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					double minimumDisplayAltitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("MinDisplayAltitude")));
					double maximumDisplayAltitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("MaxDisplayAltitude")));

					string pathsDirectory = getInnerTextFromFirstChild(iter.Current.Select("PathsDirectory"));

					if (!Path.IsPathRooted(pathsDirectory)) {
						Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), pathsDirectory);
					}

					string winColorName = getInnerTextFromFirstChild(iter.Current.Select("WinColorName"));
					Color c = Color.White;

					if (winColorName != null) {
						c = Color.FromName(winColorName);
					}
					else {
						c = getColor(iter.Current.Select("RGBColor"));
					}

					PathList pl = new PathList(name, parentWorld, minimumDisplayAltitude, maximumDisplayAltitude, pathsDirectory, distanceAboveSurface, c, parentWorld.TerrainAccessor);

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), pl);

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (pl.MetaData.Contains("InfoUri")) {
							pl.MetaData["InfoUri"] = infoUri;
						}
						else {
							pl.MetaData.Add("InfoUri", infoUri);
						}
					}

					pl.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
					pl.ParentList = parentRenderable;

					if (World.Settings.useDefaultLayerStates) {
						pl.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						pl.IsOn = IsLayerOn(pl);
					}
					parentRenderable.ChildObjects.Add(pl);

					parentRenderable.RenderPriority = RenderPriority.LinePaths;
				}
			}
		}

		private static void addLineFeature(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));

					string distanceAboveSurfaceString = getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface"));
					string minimumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MinimumDisplayAltitude"));
					string maximumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MaximumDisplayAltitude"));
					string opacityString = getInnerTextFromFirstChild(iter.Current.Select("Opacity"));
					string extrudeHeightString = getInnerTextFromFirstChild(iter.Current.Select("ExtrudeHeight"));
					string extrudeUpwardsString = getInnerTextFromFirstChild(iter.Current.Select("ExtrudeUpwards"));
					string imageUri = getInnerTextFromFirstChild(iter.Current.Select("ImageUri"));
					string outlineString = getInnerTextFromFirstChild(iter.Current.Select("Outline"));

					XPathNodeIterator posListIter = iter.Current.Select("LineString/posList");
					posListIter.MoveNext();

					string lineString = getInnerTextFromFirstChild(posListIter);

					string[] lineParts = lineString.Split(' ');
					Point3d[] points = new Point3d[lineParts.Length];

					for (int i = 0; i < lineParts.Length; i++) {
						string[] pointParts = lineParts[i].Split(',');
						points[i] = new Point3d();
						points[i].X = ParseDouble(pointParts[0]);
						points[i].Y = ParseDouble(pointParts[1]);

						if (pointParts.Length > 2) {
							points[i].Z = ParseDouble(pointParts[2]);
						}
					}

					Color c = Color.Black;
					Color outlineColor = Color.Black;

					if (iter.Current.Select("RGBColor").Count > 0) {
						c = getColor(iter.Current.Select("RGBColor"));
					}

					if (iter.Current.Select("FeatureColor").Count > 0) {
						c = getColor(iter.Current.Select("FeatureColor"));
					}

					if (iter.Current.Select("OutlineColor").Count > 0) {
						outlineColor = getColor(iter.Current.Select("OutlineColor"));
					}

					LineFeature lf = null;

					if (imageUri != null
					    && imageUri.Length > 0) {
						lf = new LineFeature(name, parentWorld, points, imageUri);
					}
					else {
						lf = new LineFeature(name, parentWorld, points, c);
					}

					lf.OutlineColor = outlineColor;

					if (outlineString != null
					    && outlineString.Length > 0) {
						lf.Outline = ParseBool(outlineString);
					}

					if (extrudeHeightString != null
					    && extrudeHeightString.Length > 0) {
						lf.ExtrudeHeight = ParseDouble(extrudeHeightString);
					}

					if (opacityString != null
					    && opacityString.Length > 0) {
						lf.Opacity = byte.Parse(opacityString);
					}

					if (distanceAboveSurfaceString != null
					    && distanceAboveSurfaceString.Length > 0) {
						lf.DistanceAboveSurface = ParseDouble(distanceAboveSurfaceString);
					}

					if (minimumDisplayAltitudeString != null
					    && minimumDisplayAltitudeString.Length > 0) {
						lf.MinimumDisplayAltitude = ParseDouble(minimumDisplayAltitudeString);
					}

					if (maximumDisplayAltitudeString != null
					    && maximumDisplayAltitudeString.Length > 0) {
						lf.MaximumDisplayAltitude = ParseDouble(maximumDisplayAltitudeString);
					}

					if (extrudeUpwardsString != null
					    && extrudeUpwardsString.Length > 0) {
						lf.ExtrudeUpwards = ParseBool(extrudeUpwardsString);
					}

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), lf);

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (lf.MetaData.Contains("InfoUri")) {
							lf.MetaData["InfoUri"] = infoUri;
						}
						else {
							lf.MetaData.Add("InfoUri", infoUri);
						}
					}

					lf.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
					lf.ParentList = parentRenderable;

					if (World.Settings.useDefaultLayerStates) {
						lf.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						lf.IsOn = IsLayerOn(lf);
					}
					parentRenderable.ChildObjects.Add(lf);

					parentRenderable.RenderPriority = RenderPriority.LinePaths;
				}
			}
		}

		private static void addPolygonFeature(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));

					string distanceAboveSurfaceString = getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface"));
					string minimumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MinimumDisplayAltitude"));
					string maximumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MaximumDisplayAltitude"));
					string opacityString = getInnerTextFromFirstChild(iter.Current.Select("Opacity"));
					string extrudeHeightString = getInnerTextFromFirstChild(iter.Current.Select("ExtrudeHeight"));
					string extrudeUpwardsString = getInnerTextFromFirstChild(iter.Current.Select("ExtrudeUpwards"));
					string imageUri = getInnerTextFromFirstChild(iter.Current.Select("ImageUri"));
					string outlineString = getInnerTextFromFirstChild(iter.Current.Select("Outline"));

					XPathNodeIterator posListIter = iter.Current.Select("exterior/LinearRing/posList");
					posListIter.MoveNext();

					string lineString = getInnerTextFromFirstChild(posListIter);

					string[] lineParts = lineString.Split(' ');
					Point3d[] points = new Point3d[lineParts.Length];

					for (int i = 0; i < lineParts.Length; i++) {
						string[] pointParts = lineParts[i].Split(',');
						points[i] = new Point3d();
						points[i].X = ParseDouble(pointParts[0]);
						points[i].Y = ParseDouble(pointParts[1]);

						if (pointParts.Length > 2) {
							points[i].Z = ParseDouble(pointParts[2]);
						}
					}

					Color c = Color.Black;
					Color outlineColor = Color.Black;

					if (iter.Current.Select("FeatureColor").Count > 0) {
						c = getColor(iter.Current.Select("FeatureColor"));
					}

					if (iter.Current.Select("OutlineColor").Count > 0) {
						outlineColor = getColor(iter.Current.Select("OutlineColor"));
					}

					PolygonFeature pf = null;

					pf = new PolygonFeature(name, parentWorld, points, c);

					pf.OutlineColor = outlineColor;

					if (outlineString != null
					    && outlineString.Length > 0) {
						pf.Outline = ParseBool(outlineString);
					}

					if (extrudeHeightString != null
					    && extrudeHeightString.Length > 0) {
						pf.ExtrudeHeight = ParseDouble(extrudeHeightString);
					}

					if (opacityString != null
					    && opacityString.Length > 0) {
						pf.Opacity = byte.Parse(opacityString);
					}

					if (distanceAboveSurfaceString != null
					    && distanceAboveSurfaceString.Length > 0) {
						pf.DistanceAboveSurface = ParseDouble(distanceAboveSurfaceString);
					}

					if (minimumDisplayAltitudeString != null
					    && minimumDisplayAltitudeString.Length > 0) {
						pf.MinimumDisplayAltitude = ParseDouble(minimumDisplayAltitudeString);
					}

					if (maximumDisplayAltitudeString != null
					    && maximumDisplayAltitudeString.Length > 0) {
						pf.MaximumDisplayAltitude = ParseDouble(maximumDisplayAltitudeString);
					}

					if (extrudeUpwardsString != null
					    && extrudeUpwardsString.Length > 0) {
						pf.ExtrudeUpwards = ParseBool(extrudeUpwardsString);
					}

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), pf);

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (pf.MetaData.Contains("InfoUri")) {
							pf.MetaData["InfoUri"] = infoUri;
						}
						else {
							pf.MetaData.Add("InfoUri", infoUri);
						}
					}

					pf.MetaData.Add("XmlSource", (string) parentRenderable.MetaData["XmlSource"]);
					pf.ParentList = parentRenderable;

					if (World.Settings.useDefaultLayerStates) {
						pf.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						pf.IsOn = IsLayerOn(pf);
					}
					parentRenderable.ChildObjects.Add(pf);

					parentRenderable.RenderPriority = RenderPriority.LinePaths;
				}
			}
		}

		#region by zzm
		private static void AddMyIcon(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					double distanceAboveSurface = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface")));
					double latitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("Latitude/Value")));
					double longitude = ParseDouble(getInnerTextFromFirstChild(iter.Current.Select("Longitude/Value")));

					string refreshString = iter.Current.GetAttribute("Refresh", "");
					string description = getInnerTextFromFirstChild(iter.Current.Select("Description"));

					string isRotatedString = getInnerTextFromFirstChild(iter.Current.Select("IsRotated"));
					string rotationDegreesString = getInnerTextFromFirstChild(iter.Current.Select("RotationDegrees"));

					string minimumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MinimumDisplayAltitude"));
					string maximumDisplayAltitudeString = getInnerTextFromFirstChild(iter.Current.Select("MaximumDisplayAltitude"));

					string clickableUrl = getInnerTextFromFirstChild(iter.Current.Select("ClickableUrl"));

					string textureFilePath = getInnerTextFromFirstChild(iter.Current.Select("TextureFilePath"));

					if (textureFilePath.Length > 0 && !Path.IsPathRooted(textureFilePath)
					    && !textureFilePath.ToLower().StartsWith("http://")) {
						// Use absolute path to icon image
						textureFilePath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), textureFilePath);
					}

					if (!Path.IsPathRooted(textureFilePath)) {
						Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), textureFilePath);
					}

					Icon ic = new Icon(name, latitude, longitude, distanceAboveSurface);

					ic.TextureFileName = textureFilePath;
					ic.Width = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("IconWidthPixels")));
					ic.Height = int.Parse(getInnerTextFromFirstChild(iter.Current.Select("IconHeightPixels")));

					if (refreshString != null
					    && refreshString.Length > 0) {
						ic.RefreshInterval = TimeSpan.FromSeconds(ParseDouble(refreshString));
					}

					string infoUri = iter.Current.GetAttribute("InfoUri", "");

					if (infoUri != null
					    && infoUri.Length > 0) {
						if (ic.MetaData.Contains("InfoUri")) {
							ic.MetaData["InfoUri"] = infoUri;
						}
						else {
							ic.MetaData.Add("InfoUri", infoUri);
						}
					}

					if (description != null) {
						ic.Description = description;
					}

					if (clickableUrl != null) {
						ic.ClickableActionURL = clickableUrl;
					}

					if (maximumDisplayAltitudeString != null) {
						ic.MaximumDisplayDistance = ParseDouble(maximumDisplayAltitudeString);
					}

					if (minimumDisplayAltitudeString != null) {
						ic.MinimumDisplayDistance = ParseDouble(minimumDisplayAltitudeString);
					}

					string onClickZoomAltString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomAltitude"));
					string onClickZoomHeadingString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomHeading"));
					string onClickZoomTiltString = getInnerTextFromFirstChild(iter.Current.Select("OnClickZoomTilt"));

					if (onClickZoomAltString != null) {
						ic.OnClickZoomAltitude = ParseDouble(onClickZoomAltString);
					}

					if (onClickZoomHeadingString != null) {
						ic.OnClickZoomHeading = ParseDouble(onClickZoomHeadingString);
					}

					if (onClickZoomTiltString != null) {
						ic.OnClickZoomTilt = ParseDouble(onClickZoomTiltString);
					}

					if (isRotatedString != null) {
						ic.IsRotated = ParseBool(isRotatedString);
					}

					if (rotationDegreesString != null) {
						if (!ic.IsRotated) {
							ic.IsRotated = true;
						}

						ic.Rotation = Angle.FromDegrees(ParseDouble(rotationDegreesString));
					}

					addExtendedInformation(iter.Current.Select("ExtendedInformation"), ic);

					ic.ParentList = parentRenderable;

					string cachePath = String.Format("{0}{1}{2}{1}{3}{1}{3}", cache.CacheDirectory, Path.DirectorySeparatorChar, getRenderablePathString(parentRenderable), name);

					ic.SaveFilePath = cachePath;

					if (World.Settings.useDefaultLayerStates) {
						ic.IsOn = ParseBool(iter.Current.GetAttribute("ShowAtStartup", ""));
					}
					else {
						ic.IsOn = IsLayerOn(ic);
					}
					ic.MetaData["XmlSource"] = (string) parentRenderable.MetaData["XmlSource"];

					MyIcons parentIconList = (MyIcons) parentRenderable;

					parentIconList.Add(ic);

					addScreenOverlaysToIcon(iter.Current.Select("ScreenOverlay"), parentWorld, ic, cache);
				}
			}
		}

		private static void AddMyChart(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string isOnStr = iter.Current.GetAttribute("ShowAtStartup", "");
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					string latStr = getInnerTextFromFirstChild(iter.Current.Select("Latitude/Value"));
					string lonStr = getInnerTextFromFirstChild(iter.Current.Select("Longitude/Value"));
					string distStr = getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface"));
					string diaStr = getInnerTextFromFirstChild(iter.Current.Select("Diameter"));
					string heightStr = getInnerTextFromFirstChild(iter.Current.Select("Height"));
					Color chartColor = getColor(iter.Current.Select("ChartColor"));
					string scaleStr = getInnerTextFromFirstChild(iter.Current.Select("Scale"));
					string typeStr = getInnerTextFromFirstChild(iter.Current.Select("ChartType"));
					string capTxt = getInnerTextFromFirstChild(iter.Current.Select("Caption/CaptionText"));
					FontDescription capFontDesc = getDisplayFont(iter.Current.Select("Caption/DisplayFont"));
					Color capColor = getColor(iter.Current.Select("CaptionColor"));
					string clkUrl = getInnerTextFromFirstChild(iter.Current.Select("ClickableUrl"));
					string minViewRng = getInnerTextFromFirstChild(iter.Current.Select("MinViewRange"));
					string maxViewRng = getInnerTextFromFirstChild(iter.Current.Select("MaxViewRange"));

					MyChart c = new MyChart(name, (float) ParseDouble(latStr), (float) ParseDouble(lonStr), (float) ParseDouble(distStr));
					c.Diameter = (float) ParseDouble(diaStr);
					c.Height = (float) ParseDouble(heightStr);
					c.ChartColor = chartColor;
					c.ChartType = typeStr;
					c.Caption = capTxt;
					c.CaptionFont = new Font(capFontDesc.FaceName, 9.0F);
					c.CaptionColor = capColor;
					c.ClickableUrl = clkUrl;
					c.MinViewRange = (float) ParseDouble(minViewRng);
					c.MaxViewRange = (float) ParseDouble(maxViewRng);
					MyCharts.WindowWidth = 1024;
					MyCharts myCharts = parentRenderable as MyCharts;
					myCharts.Add(c);

					c.ParentWorld = parentWorld;
					c.ParentList = myCharts;
					c.Scale = (float) ParseDouble(scaleStr);
				}
			}
		}

		private static void AddMesh(XPathNodeIterator iter, World parentWorld, RenderableObjectList parentRenderable, Cache cache) {
			if (iter.Count > 0) {
				while (iter.MoveNext()) {
					string isOn = iter.Current.GetAttribute("ShowAtStartup", "");
					string name = getInnerTextFromFirstChild(iter.Current.Select("Name"));
					string distAbvSfc = getInnerTextFromFirstChild(iter.Current.Select("DistanceAboveSurface"));
					string lat = getInnerTextFromFirstChild(iter.Current.Select("Latitude/Value"));
					string lon = getInnerTextFromFirstChild(iter.Current.Select("Longitude/Value"));
					string rotX = getInnerTextFromFirstChild(iter.Current.Select("Orientation/RotationX"));
					string rotY = getInnerTextFromFirstChild(iter.Current.Select("Orientation/RotationY"));
					string rotZ = getInnerTextFromFirstChild(iter.Current.Select("Orientation/RotationZ"));
					string scale = getInnerTextFromFirstChild(iter.Current.Select("ScaleFactor"));
					string minView = getInnerTextFromFirstChild(iter.Current.Select("MinViewRange"));
					string maxView = getInnerTextFromFirstChild(iter.Current.Select("MaxViewRange"));
					string filePath = getInnerTextFromFirstChild(iter.Current.Select("MeshFilePath"));

					MeshLayer meshLayer = new MeshLayer(name, (float) ParseDouble(lat), (float) ParseDouble(lon), 6378137.0F, (float) ParseDouble(scale), filePath, new Quaternion());
					meshLayer.ParentList = parentRenderable;
					meshLayer.IsOn = ParseBool(isOn);
					parentRenderable.Add(meshLayer);
				}
			}
		}
		#endregion

		private static TerrainAccessor[] getTerrainAccessorsFromXPathNodeIterator(XPathNodeIterator iter, string cacheDirectory) {
			ArrayList terrainAccessorList = new ArrayList();

			while (iter.MoveNext()) {
				string terrainAccessorName = iter.Current.GetAttribute("Name", "");
				if (terrainAccessorName == null) {
					// TODO: Throw exception?
					continue;
				}

				XPathNodeIterator latLonBoxIter = iter.Current.Select("LatLonBoundingBox");
				if (latLonBoxIter.Count != 1) {
					// TODO: Throw exception?
					continue;
				}

				double north = 0;
				double south = 0;
				double west = 0;
				double east = 0;

				latLonBoxIter.MoveNext();

				north = ParseDouble(getInnerTextFromFirstChild(latLonBoxIter.Current.Select("North")));
				south = ParseDouble(getInnerTextFromFirstChild(latLonBoxIter.Current.Select("South")));
				west = ParseDouble(getInnerTextFromFirstChild(latLonBoxIter.Current.Select("West")));
				east = ParseDouble(getInnerTextFromFirstChild(latLonBoxIter.Current.Select("East")));

				TerrainAccessor[] higerResolutionSubsets = getTerrainAccessorsFromXPathNodeIterator(iter.Current.Select("HigherResolutionSubsets"), Path.Combine(cacheDirectory, terrainAccessorName));

				XPathNodeIterator tileServiceIter = iter.Current.Select("TerrainTileService");
				if (tileServiceIter.Count == 1) {
					string serverUrl = null;
					string dataSetName = null;
					double levelZeroTileSizeDegrees = double.NaN;
					uint numberLevels = 0;
					uint samplesPerTile = 0;
					string dataFormat = null;
					string fileExtension = null;
					string compressionType = null;

					tileServiceIter.MoveNext();

					serverUrl = getInnerTextFromFirstChild(tileServiceIter.Current.Select("ServerUrl"));
					dataSetName = getInnerTextFromFirstChild(tileServiceIter.Current.Select("DataSetName"));
					levelZeroTileSizeDegrees = ParseDouble(getInnerTextFromFirstChild(tileServiceIter.Current.Select("LevelZeroTileSizeDegrees")));
					numberLevels = uint.Parse(getInnerTextFromFirstChild(tileServiceIter.Current.Select("NumberLevels")));
					samplesPerTile = uint.Parse(getInnerTextFromFirstChild(tileServiceIter.Current.Select("SamplesPerTile")));
					dataFormat = getInnerTextFromFirstChild(tileServiceIter.Current.Select("DataFormat"));
					fileExtension = getInnerTextFromFirstChild(tileServiceIter.Current.Select("FileExtension"));
					compressionType = getInnerTextFromFirstChild(tileServiceIter.Current.Select("CompressonType"));

					TerrainTileService tts = new TerrainTileService(serverUrl, dataSetName, levelZeroTileSizeDegrees, (int) samplesPerTile, fileExtension, (int) numberLevels, Path.Combine(cacheDirectory, terrainAccessorName), World.Settings.TerrainTileRetryInterval, dataFormat);

					TerrainAccessor newTerrainAccessor = new NltTerrainAccessor(terrainAccessorName, west, south, east, north, tts, higerResolutionSubsets);

					terrainAccessorList.Add(newTerrainAccessor);
				}
				//TODO: Add Floating point terrain Accessor code
				//TODO: Add WMSAccessor code and make it work in TerrainAccessor (which it currently doesn't)
			}

			if (terrainAccessorList.Count > 0) {
				return (TerrainAccessor[]) terrainAccessorList.ToArray(typeof (TerrainAccessor));
			}
			else {
				return null;
			}
		}

		private static string getInnerTextFromFirstChild(XPathNodeIterator iter) {
			if (iter.Count == 0) {
				return null;
			}
			else {
				iter.MoveNext();
				return GetCleanText(iter.Current.Value);
			}
		}

		private static string GetCleanText(string s) {
			//string tmp = s.ToLower();
			s = s.Replace("<B>", " ");
			s = s.Replace("</B>", " ");
			s = s.Replace("<BR/>", "\r\n");
			return s;
		}
	}
}