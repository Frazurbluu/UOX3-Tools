using System;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace CharacterExporter
{
	public enum KeepItemsState
	{
		KEEP_ALL		= 0,
		KEEP_NONE		= 1,
		KEEP_WEARABLES	= 2,
		KEEP_PACKITEMS	= 3
	};

    public partial class CharacterViewer : Form
    {
        public CharacterViewer()
        {
            InitializeComponent();
        }

        private void txtDirPath_TextChanged(object sender, EventArgs e)
        {
			folderBrowserDialog.SelectedPath = txtDirPath.Text;
			fileBrowserDialog.InitialDirectory = txtDirPath.Text;
        }

        private void comboCharList_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboItemList.Items.Clear();

            CharObject mChar = ( CharObject )worldObjects.CharList[comboCharList.SelectedIndex];
            if( mChar != null )
                ListItemsInCont(mChar);
        }

        private void ListItemsInCont( WorldObject mCont )
        {
            foreach( ItemObject mItem in mCont.ContainsList )
            {
                comboItemList.Items.Add(mItem.Name + " (" + UOXData.Conversion.ToHexString( mItem.ID ) + ", " + UOXData.Conversion.ToHexString( mItem.Serial ) + ")" );
                ListItemsInCont(mItem);
            }
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            fileBrowserDialog.Filter = "Character Export Files (*.cef)|*.cef|All Files (*.*)|*.*";
            if( fileBrowserDialog.ShowDialog() == DialogResult.OK )
            {
				foreach( string filePath in fileBrowserDialog.FileNames )
                {
					if( File.Exists( filePath ) )
					{
						UOXData.Script.WorldFile90 importWorld = new UOXData.Script.WorldFile90( filePath );
						if( importWorld.Sections.Count > 0 )
						{
							LoadWorldFile( importWorld, true );
							worldFiles.Add(importWorld);
						}
					}
				}

                foreach( ItemObject mItem in worldObjects.ImportItemList )
                {
					WorldObject mObj = worldObjects.FindObjectBySerial(mItem.Container, true);
					if (mObj != null)
						mObj.ContainsList.Add(mItem);
				}
				foreach( CharObject mChar in worldObjects.ImportCharList )
				{
					bool contChanged = false;
					if( mChar.Serial < worldObjects.NextCharSer )
					{
						contChanged = true;
						mChar.Serial = worldObjects.NextCharSer;
						UOXData.Script.TagDataPair serTag = mChar.Section.GetDataPair( "Serial" );
						serTag.Data = new UOXData.Script.DataValue( UOXData.Conversion.ToHexString( mChar.Serial ) );
                        ++worldObjects.NextCharSer;
					}
					CheckItemsInCont( mChar, contChanged );
					worldObjects.CharList.Add(mChar);
					comboCharList.Items.Add(mChar.Name + " (" + UOXData.Conversion.ToHexString(mChar.Serial) + ")");
                }
				worldObjects.ImportCharList.Clear();
				worldObjects.ImportItemList.Clear();
            }
        }

        private void CheckItemsInCont( WorldObject mObj, bool cChange )
        {
            foreach( ItemObject mItem in mObj.ContainsList )
            {
                if( cChange )
                {
                    mItem.Container = mObj.Serial;
                    UOXData.Script.TagDataPair contTag = mItem.Section.GetDataPair("Cont");
                    contTag.Data = new UOXData.Script.DataValue(UOXData.Conversion.ToHexString(mObj.Serial));
                }
                bool contChanged = false;
                if( mItem.Serial < worldObjects.NextItemSer )
                {
                    contChanged = true;
                    mItem.Serial = worldObjects.NextItemSer;
                    UOXData.Script.TagDataPair serTag = mItem.Section.GetDataPair("Serial");
                    serTag.Data = new UOXData.Script.DataValue(UOXData.Conversion.ToHexString(mItem.Serial));
                    ++worldObjects.NextItemSer;
                }
                CheckItemsInCont( mItem, contChanged );
                worldObjects.ItemList.Add( mItem );
            }
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            if( comboCharList.Items.Count > 0 && comboCharList.SelectedIndices.Count > 0 )
            {
				folderBrowserDialog.Description = "Select the Location to Export your character(s) to.";
				if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
				{
					progressBar.Minimum = 0;
					progressBar.Value = 0;
					progressBar.Maximum = comboCharList.SelectedIndices.Count;
					foreach( int mSelection in comboCharList.SelectedIndices )
					{
						CharObject mChar = ( CharObject )worldObjects.CharList[mSelection];
						if( mChar != null )
						{
							string fileName = mChar.Name + "_" + UOXData.Conversion.ToHexString(mChar.Serial) + ".cef";
							string filePath = folderBrowserDialog.SelectedPath + "\\" + fileName;

							StreamWriter fileOut = File.CreateText(filePath);
							mChar.Section.Save( fileOut );
							if( keepItemState != KeepItemsState.KEEP_NONE )
								SaveItemsInCont( mChar, fileOut );
							fileOut.Close();
							progressBar.PerformStep();
						}
					}
				}
            }
        }

        private void SaveItemsInCont(WorldObject mCont, StreamWriter fileOut)
        {
            foreach (ItemObject mItem in mCont.ContainsList)
            {
				if( mCont.ObjType != ObjectType.OT_CHAR || keepItemState != KeepItemsState.KEEP_PACKITEMS )
					mItem.Section.Save(fileOut);
				if( keepItemState != KeepItemsState.KEEP_WEARABLES )
	                SaveItemsInCont(mItem, fileOut);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = folderBrowserDialog.SelectedPath + "\\0.0.wsc";

                StreamWriter fileOut = File.CreateText(filePath);
                foreach( UOXData.Script.WorldFile90 mWorld in worldFiles )
                {
                    mWorld.Save( fileOut );
                }
                fileOut.Close();
                MessageBox.Show("Successfully saved as 0.0.wsc", "Success");
            }
        }

		private void LoadWorldFile( UOXData.Script.WorldFile90 toAdd, bool doImport )
		{
			foreach (UOXData.Script.WorldSection mScript in toAdd.Sections)
			{
				if (mScript.SectionName == "CHARACTER")
				{
					uint charSerial = UOXData.Conversion.ToUInt32(mScript.FindTag("Serial"));
					if (charSerial >= worldObjects.NextCharSer)
						worldObjects.NextCharSer = charSerial + 1;
					CharObject mChar = new CharObject(charSerial);
					mChar.Name = mScript.FindTag("Name");
					mChar.ID = UOXData.Conversion.ToUInt16(mScript.FindTag("ID"));
					mChar.Section = mScript;
					if( doImport )
						worldObjects.ImportCharList.Add( mChar );
					else
						worldObjects.CharList.Add( mChar );
				}
				else
				{
					uint contSerial = UOXData.Conversion.ToUInt32(mScript.FindTag("Cont"));
					uint itemSerial = UOXData.Conversion.ToUInt32(mScript.FindTag("Serial"));
					if (itemSerial >= worldObjects.NextItemSer)
						worldObjects.NextItemSer = itemSerial + 1;
					ItemObject mItem = new ItemObject(itemSerial);
					if (contSerial != 0xFFFFFFFF)
					{
						mItem.Container = contSerial;
						mItem.Name = mScript.FindTag("Name");
						mItem.Section = mScript;
						mItem.ID = UOXData.Conversion.ToUInt16(mScript.FindTag("ID"));
					}
					if( doImport )
						worldObjects.ImportItemList.Add(mItem);
					else
						worldObjects.ItemList.Add( mItem );
				}
			}
		}
        private void btnBrowse_Click(object sender, EventArgs e)
        {
			folderBrowserDialog.Description = "Please Select the UOX3 Save File Directory to Load.";
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK )
            {
				string dirPath = folderBrowserDialog.SelectedPath;
                worldFiles.Clear();
				worldObjects.Clear();
				comboCharList.Items.Clear();
				comboItemList.Items.Clear();
                txtDirPath.Text = dirPath;
				comboCharList.Update();
				comboItemList.Update();
				txtDirPath.Update();

				string[] fileList = Directory.GetFiles( dirPath, "*.*.wsc" );
				if( fileList.Length > 0 )
				{
					progressBar.Minimum = 0;
					progressBar.Value = 1;
					foreach( string mFile in fileList )
					{
						UOXData.Script.WorldFile90 toAdd = new UOXData.Script.WorldFile90( mFile );
						LoadWorldFile( toAdd, false );
						worldFiles.Add(toAdd);
					}
					progressBar.Maximum = 1 + worldObjects.CharList.Count + worldObjects.ItemList.Count;
					foreach (CharObject mChar in worldObjects.CharList)
					{
						comboCharList.Items.Add(mChar.Name + " (" + UOXData.Conversion.ToHexString(mChar.Serial) + ")");
						progressBar.PerformStep();
					}
					foreach( ItemObject mItem in worldObjects.ItemList )
					{
						WorldObject mObj = worldObjects.FindObjectBySerial( mItem.Container, false );
						if( mObj != null )
							mObj.ContainsList.Add( mItem );
						progressBar.PerformStep();
					}

					if( comboCharList.Items.Count > 0 )
						comboCharList.SelectedIndex = 0;
				}
				else
					MessageBox.Show("No worldfile found, please select a valid directory", "File Not Found" );

            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

		private void radioAll_CheckedChanged(object sender, EventArgs e)
		{
			keepItemState = KeepItemsState.KEEP_ALL;
		}

		private void radioNone_CheckedChanged(object sender, EventArgs e)
		{
			keepItemState = KeepItemsState.KEEP_NONE;
		}

		private void radioWearables_CheckedChanged(object sender, EventArgs e)
		{
			keepItemState = KeepItemsState.KEEP_WEARABLES;
		}

		private void radioPack_CheckedChanged(object sender, EventArgs e)
		{
			keepItemState = KeepItemsState.KEEP_PACKITEMS;
		}
    }
}