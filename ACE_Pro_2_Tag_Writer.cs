
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ACEPro2TagWriter
{
    public class SafeProfileListBox : ListBox
    {
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Scroll the list but never change the selected profile.
            int oldSelected=SelectedIndex;
            base.OnMouseWheel(e);
            if(SelectedIndex!=oldSelected) SelectedIndex=oldSelected;
        }
    }

    public class MainForm : Form
    {
        ComboBox cbReader = new ComboBox();
        SafeProfileListBox lbProfiles = new SafeProfileListBox();
        ComboBox txtManufacturer = new ComboBox();
        TextBox txtCode = new TextBox();
        ComboBox txtMaterial = new ComboBox();
        TextBox txtSku = new TextBox();
        TextBox txtColor = new TextBox();
        NumericUpDown nNozMin = new NumericUpDown(), nNozMax = new NumericUpDown();
        NumericUpDown nBedMin = new NumericUpDown(), nBedMax = new NumericUpDown();
        NumericUpDown nWeight = new NumericUpDown(), nLength = new NumericUpDown();
        Label lblUid = new Label();
        Label lblStatus = new Label();
        TextBox txtLog = new TextBox();
        Button btnColor = new Button();
        Button btnSkuFromUid = new Button();
        TextBox txtProfileName = new TextBox();
        NumericUpDown nEmptySpool = new NumericUpDown();
        NumericUpDown nCurrentGross = new NumericUpDown();
        NumericUpDown nPrintNeed = new NumericUpDown();
        NumericUpDown nSafetyMargin = new NumericUpDown();
        Label lblRemaining = new Label();
        Label lblPrintCheck = new Label();
                ComboBox cbColourName = new ComboBox();
        TextBox txtManufacturerColour = new TextBox();
        Panel pnlColourSwatch = new Panel();
        NumericUpDown nWriteQty = new NumericUpDown();
        NumericUpDown nNewGross = new NumericUpDown();
        bool dirty = false;
        bool loadingProfile = false;
        string editingProfileKey = ""; // exact saved record currently being edited
        Button btnDeleteProfile = new Button();
        ContextMenuStrip profileMenu = new ContextMenuStrip();
        Button btnCloseProfile = new Button();
        Label lblFumeWarning = new Label();
        Button btnExportGitHub = new Button();
        Button btnExportReels = new Button();
        Button btnShowReels = new Button();
        readonly Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();

        public MainForm()
        {
            Text = "ACE Pro 2 RFID Tag Writer v1.3b";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1020; Height = 1130;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);


            int y=18;
            AddLabel("Reader", 20,y); cbReader.SetBounds(160,y,420,25); Controls.Add(cbReader);
            var btnRefresh = new Button(){Text="Refresh"}; btnRefresh.SetBounds(590,y,90,26); btnRefresh.Click += (s,e)=>RefreshReaders(); Controls.Add(btnRefresh); y+=38;

            AddLabel("Profiles",20,y);
            lbProfiles.SetBounds(160,y,380,92);
            lbProfiles.IntegralHeight=false;
            Controls.Add(lbProfiles);
            lbProfiles.SelectedIndexChanged += (s,e)=>ProfileSelectionChanged();
            lbProfiles.MouseDown += ProfileListMouseDown;

            var btnReloadProfiles = new Button(){Text="Reload"}; btnReloadProfiles.SetBounds(550,y,80,26); btnReloadProfiles.Click += (s,e)=>{LoadGlobalProfiles();LoadSavedProfiles();RefreshProfileList();}; Controls.Add(btnReloadProfiles);
            btnDeleteProfile.Text="Delete"; btnDeleteProfile.SetBounds(635,y,65,26); btnDeleteProfile.Click += DeleteProfile; Controls.Add(btnDeleteProfile);
            btnCloseProfile.Text="Close"; btnCloseProfile.SetBounds(705,y,65,26); btnCloseProfile.Click += CloseProfile; Controls.Add(btnCloseProfile);
            y+=104;

            AddLabel("Profile",20,y);
            txtProfileName.SetBounds(160,y,380,25);
            txtProfileName.ReadOnly=true;
            txtProfileName.TabStop=false;
            Controls.Add(txtProfileName);
            y+=38;

            AddComboField("Manufacturer", txtManufacturer, ref y);
            AddComboField("Material", txtMaterial, ref y);

            // Tag Code and SKU are protocol fields. They are retained internally for
            // compatibility/imported profiles but intentionally hidden from normal use.
            txtCode.Visible=false;
            txtSku.Visible=false;
            btnSkuFromUid.Visible=false;

            AddLabel("Manufacturer colour",20,y);
            txtManufacturerColour.SetBounds(160,y,300,25); Controls.Add(txtManufacturerColour);
            var manufacturerHelp=new Label(){Text="e.g. Bone White"}; manufacturerHelp.SetBounds(470,y+4,220,22); Controls.Add(manufacturerHelp); y+=38;

            AddLabel("HEX colour",20,y);
            // Normal user-facing RGB HEX. ACE ABGR conversion is handled internally.
            txtColor.SetBounds(160,y,170,25); txtColor.Text="#"; Controls.Add(txtColor);
            txtColor.TextChanged += (s,e)=>HexChanged();

            pnlColourSwatch.SetBounds(340,y,34,25);
            pnlColourSwatch.BorderStyle=BorderStyle.FixedSingle;
            pnlColourSwatch.BackColor=SystemColors.Control;
            Controls.Add(pnlColourSwatch);

            btnColor.Text="Pick colour"; btnColor.SetBounds(385,y,110,26); btnColor.Click += PickColor; Controls.Add(btnColor);
            var lblClosest=new Label(){Name="lblClosestColour",Text=""}; lblClosest.SetBounds(505,y+4,300,22); Controls.Add(lblClosest); y+=38;

            AddNumericRow("Nozzle °C", nNozMin, nNozMax, ref y, 0, 400);
            AddNumericRow("Bed °C", nBedMin, nBedMax, ref y, 0, 200);
            // Protocol/tag filament weight remains internal. The user-facing reel weight is gross weight.
            ConfigureNum(nWeight,0,10000);
            nWeight.Value=1000;
            // Length is protocol housekeeping only: hidden from normal entry.
            ConfigureNum(nLength,0,5000);
            nLength.Value=330;

            // Reel measurements belong to the profile.
            AddSingleNumeric("New reel gross g", nNewGross, ref y, 0, 15000);
            nNewGross.Value=1200;
            AddSingleNumeric("Empty spool g", nEmptySpool, ref y, 0, 5000);

            var btnSave = new Button(){Text="Save Profile"}; btnSave.SetBounds(160,y,130,36); btnSave.Click += SaveProfile; Controls.Add(btnSave);
            var lblExisting=new Label(){Name="lblExistingProfile",Text=""}; lblExisting.SetBounds(300,y+8,480,24); Controls.Add(lblExisting); y+=46;

            // Tag operations are kept together above the reel calculator.
            lblUid.SetBounds(20,y,720,25); lblUid.Text="UID: —"; Controls.Add(lblUid); y+=28;
            lblStatus.SetBounds(20,y,720,25); lblStatus.Text="Tag status: —"; Controls.Add(lblStatus); y+=38;

            AddLabel("Write quantity",20,y); ConfigureNum(nWriteQty,1,100); nWriteQty.Value=1; nWriteQty.SetBounds(160,y,110,25); Controls.Add(nWriteQty);
            var qHelp=new Label(){Text="Write the same profile to several tags"}; qHelp.SetBounds(280,y+4,280,22); Controls.Add(qHelp); y+=34;

            var btnRead = new Button(){Text="Read Tag"}; btnRead.SetBounds(20,y,140,36); btnRead.Click += ReadTag; Controls.Add(btnRead);
            var btnDiagnostic = new Button(){Text="Copy Diagnostic"}; btnDiagnostic.SetBounds(170,y,140,36); btnDiagnostic.Click += CopyDiagnostic; Controls.Add(btnDiagnostic);
            var btnWrite = new Button(){Text="Write + Verify"}; btnWrite.SetBounds(320,y,140,36); btnWrite.Click += WriteTag; Controls.Add(btnWrite);
            var btnVerify = new Button(){Text="Verify Only"}; btnVerify.SetBounds(470,y,140,36); btnVerify.Click += VerifyTag; Controls.Add(btnVerify);
            y+=44;

            btnExportGitHub.Text="Export for GitHub"; btnExportGitHub.SetBounds(20,y,140,36); btnExportGitHub.Click += ExportForGitHub; Controls.Add(btnExportGitHub);
            btnShowReels.Text="Reel Records"; btnShowReels.SetBounds(170,y,140,36); btnShowReels.Click += ShowReelRecords; Controls.Add(btnShowReels);
            btnExportReels.Text="Export Reels CSV"; btnExportReels.SetBounds(320,y,140,36); btnExportReels.Click += ExportReelsCsv; Controls.Add(btnExportReels);
            var btnAbout = new Button(){Text="About"}; btnAbout.SetBounds(470,y,140,36); btnAbout.Click += ShowAbout; Controls.Add(btnAbout);
            y+=48;

            var wTitle=new Label(){Text="Reel weight check (calculator only — does not alter the NFC tag)",Font=new Font(Font,FontStyle.Bold)};
            wTitle.SetBounds(20,y,650,24); Controls.Add(wTitle); y+=28;
            AddSingleNumeric("Current gross g", nCurrentGross, ref y, 0, 15000);
            AddSingleNumeric("Print needs g", nPrintNeed, ref y, 0, 10000);
            AddSingleNumeric("Safety margin g", nSafetyMargin, ref y, 0, 5000);
            nSafetyMargin.Value=50;
            nNewGross.ValueChanged += (s,e)=>UpdateWeightCheck();
            nEmptySpool.ValueChanged += (s,e)=>UpdateWeightCheck();
            nCurrentGross.ValueChanged += (s,e)=>UpdateWeightCheck();
            nPrintNeed.ValueChanged += (s,e)=>UpdateWeightCheck();
            nSafetyMargin.ValueChanged += (s,e)=>UpdateWeightCheck();
            lblRemaining.SetBounds(20,y,720,24); lblRemaining.Text="Remaining: —"; Controls.Add(lblRemaining); y+=25;
            lblPrintCheck.SetBounds(20,y,720,24); lblPrintCheck.Text="Print check: —"; Controls.Add(lblPrintCheck); y+=30;

            txtLog.Multiline=true; txtLog.ScrollBars=ScrollBars.Vertical; txtLog.ReadOnly=true;
            txtLog.SetBounds(20,y,800,150); Controls.Add(txtLog);

            SeedChoiceLists();
            LoadGlobalProfiles();
            LoadSavedProfiles();
            RefreshReaders();
            if(cbReader.Items.Count==0) {
                MessageBox.Show("No ACR122U / PC-SC reader was detected.\r\n\r\nConnect the reader, then click Refresh.",
                                "Reader not connected",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }

            // Start blank except for the detected reader.
            lbProfiles.ClearSelected();
            ClearFilamentFields();

            WireDirtyTracking();
            FormClosing += MainForm_FormClosing;
        }

        void ShowAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "ACE Pro 2 Tag Writer v1.3b\r\n\r\n" +
                "Copyright © 2026 MadeToFitUK\r\n" +
                "Licensed under the MIT License\r\n\r\n" +
                "Independent community project — not affiliated with or endorsed by Anycubic.\r\n\r\n" +
                "GitHub: https://github.com/MadeToFitUK/ACE-Pro-2-Tag-Writer",
                "About ACE Pro 2 Tag Writer",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        void AddLabel(string t,int x,int y){ var l=new Label(){Text=t}; l.SetBounds(x,y+4,135,22); Controls.Add(l); }
        void AddField(string name, TextBox box, ref int y){ AddLabel(name,20,y); box.SetBounds(160,y,300,25); Controls.Add(box); y+=38; }
        void AddComboField(string name, ComboBox box, ref int y)
        {
            AddLabel(name,20,y);
            box.SetBounds(160,y,300,25);
            box.DropDownStyle=ComboBoxStyle.DropDown;
            box.AutoCompleteMode=AutoCompleteMode.SuggestAppend;
            box.AutoCompleteSource=AutoCompleteSource.ListItems;
            Controls.Add(box);
            y+=38;
        }
        void AddNumericRow(string name, NumericUpDown a, NumericUpDown b, ref int y, int min, int max)
        {
            AddLabel(name,20,y); ConfigureNum(a,min,max); ConfigureNum(b,min,max);
            a.SetBounds(160,y,100,25); b.SetBounds(290,y,100,25); Controls.Add(a); Controls.Add(b);
            var lab=new Label(){Text="to"}; lab.SetBounds(266,y+4,20,22); Controls.Add(lab); y+=38;
        }
        void AddSingleNumeric(string name, NumericUpDown a, ref int y, int min, int max)
        {
            AddLabel(name,20,y); ConfigureNum(a,min,max); a.SetBounds(160,y,300,25); Controls.Add(a); y+=38;
        }
        void ConfigureNum(NumericUpDown n,int min,int max){ n.Minimum=min; n.Maximum=max; }

        void Log(string s)
        {
            bool followNewest = txtLog.SelectionStart >= Math.Max(0,txtLog.TextLength-4);
            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+s+Environment.NewLine);
            if(followNewest) {
                txtLog.SelectionStart=txtLog.TextLength;
                txtLog.ScrollToCaret();
            }
        }

        string SelectedProfileKey()
        {
            if(lbProfiles.SelectedItem==null) return "";
            string display=lbProfiles.SelectedItem.ToString();
            if(display.StartsWith("✓ ")) display=display.Substring(2);
            return display;
        }

        void ProfileSelectionChanged()
        {
            if(loadingProfile || lbProfiles.SelectedItem==null) return;

            if(dirty) {
                var answer=MessageBox.Show(
                    "You have unsaved changes.\r\n\r\nLoad another profile and discard those changes?",
                    "Unsaved changes",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if(answer!=DialogResult.Yes) {
                    loadingProfile=true;
                    lbProfiles.ClearSelected();
                    loadingProfile=false;
                    return;
                }
            }
            LoadProfile();
        }

        void LoadProfile()
        {
            string key=SelectedProfileKey();
            if(key=="") return;
            Profile p;
            if(!profiles.TryGetValue(key,out p)) return;

            loadingProfile=true;
            txtManufacturer.Text=p.Manufacturer;
            txtMaterial.Text=p.Material;
            txtManufacturerColour.Text=p.ManufacturerColour;
            txtCode.Text=p.Code;
            txtSku.Text=p.Sku;
            txtColor.Text=string.IsNullOrWhiteSpace(p.Color) ? "#" : p.Color;
            nNozMin.Value=p.NozzleMin; nNozMax.Value=p.NozzleMax;
            nBedMin.Value=p.BedMin; nBedMax.Value=p.BedMax;
            nWeight.Value=p.Weight;
            int protocolLength = p.Length > 0 ? p.Length : 330;
            nLength.Value=Math.Min(nLength.Maximum,Math.Max(nLength.Minimum,protocolLength));
            // Persistent reel/profile measurements: preserve known new-gross and empty-spool values.
            // Current gross is deliberately NOT changed when selecting a profile.
            if(p.EmptySpool>=0 && p.EmptySpool<=5000) nEmptySpool.Value=p.EmptySpool;
            if(p.NewGross>0 && p.NewGross<=15000) nNewGross.Value=p.NewGross; else nNewGross.Value=1200;
            UpdateGeneratedProfileName();
            UpdateWeightCheck();
            UpdateFumeWarning();
            loadingProfile=false;
            editingProfileKey=key;
            dirty=false;
        }

        string GeneratedProfileName()
        {
            string maker=(txtManufacturer.Text ?? "").Trim();
            string colour=(txtManufacturerColour.Text ?? "").Trim();
            string material=(txtMaterial.Text ?? "").Trim();

            var parts=new List<string>();
            if(maker!="") parts.Add(maker);
            if(colour!="") parts.Add(colour);
            if(material!="") parts.Add(material);
            return string.Join(" ",parts.ToArray());
        }

        void UpdateGeneratedProfileName()
        {
            txtProfileName.Text=GeneratedProfileName();
        }

        static bool ComboContainsIgnoreCase(ComboBox box, string value)
        {
            foreach(object item in box.Items)
                if(string.Equals(Convert.ToString(item), value, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        void SeedChoiceLists()
        {
            // Keep only universal starter choices here. Manufacturer names from saved
            // profiles are added below, preserving the user's chosen capitalisation.
            foreach(string s in new string[]{"Anycubic","eSUN"})
                if(!ComboContainsIgnoreCase(txtManufacturer,s)) txtManufacturer.Items.Add(s);

            foreach(string s in new string[]{"PLA","PLA+","PETG","ASA","ABS","TPU","PA","Nylon","PC"})
                if(!ComboContainsIgnoreCase(txtMaterial,s)) txtMaterial.Items.Add(s);
        }

        void RefreshChoiceListsFromProfiles()
        {
            foreach(Profile p in profiles.Values) {
                if(!string.IsNullOrWhiteSpace(p.Manufacturer) && !ComboContainsIgnoreCase(txtManufacturer,p.Manufacturer))
                    txtManufacturer.Items.Add(p.Manufacturer);
                if(!string.IsNullOrWhiteSpace(p.Material) && !ComboContainsIgnoreCase(txtMaterial,p.Material))
                    txtMaterial.Items.Add(p.Material);
            }
        }

        static string ProtocolCodeForManufacturer(string manufacturer)
        {
            var sb=new StringBuilder();
            foreach(char ch in (manufacturer??"").ToUpperInvariant())
                if(char.IsLetterOrDigit(ch)) sb.Append(ch);
            string s=sb.ToString();
            if(s=="") s="GENERIC";
            return s.Length<=8 ? s : s.Substring(0,8);
        }

        static string ProtocolSkuForProfile(string manufacturer,string color)
        {
            string maker=ProtocolCodeForManufacturer(manufacturer);
            string hex=(color??"").Replace("#","").ToUpperInvariant();
            bool validHex=hex.Length==6;
            if(validHex) foreach(char ch in hex) if(!Uri.IsHexDigit(ch)) { validHex=false; break; }
            if(!validHex) hex="000000";
            string s=maker+"-"+hex;
            return s.Length<=12 ? s : s.Substring(0,12);
        }

        int ProtocolFilamentWeight()
        {
            int gross=(int)nNewGross.Value;
            int empty=(int)nEmptySpool.Value;
            if(gross>empty && empty>0) return gross-empty;
            int existing=(int)nWeight.Value;
            return existing>0 ? existing : 1000;
        }

        Profile Current()
        {
            string manufacturer=txtManufacturer.Text.Trim();
            string manufacturerColour=txtManufacturerColour.Text.Trim();
            string material=txtMaterial.Text.Trim();
            string code=txtCode.Text.Trim();
            string sku=txtSku.Text.Trim();

            // Retire identifiers left behind by the old colour experiments.
            // For third-party profiles, generate a stable hidden identity from
            // Manufacturer + RGB. Genuine Anycubic AC/SKU values are preserved.
            bool staleTest = string.Equals(code,"TEST",StringComparison.OrdinalIgnoreCase) ||
                             (sku??"").StartsWith("TEST",StringComparison.OrdinalIgnoreCase);
            if(!string.Equals(manufacturer,"Anycubic",StringComparison.OrdinalIgnoreCase) &&
               (staleTest || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(sku))) {
                code=ProtocolCodeForManufacturer(manufacturer);
                sku=ProtocolSkuForProfile(manufacturer,txtColor.Text.Trim());
            }
            int protocolLength=(int)nLength.Value;
            if(staleTest && protocolLength==100) protocolLength=330;

            return new Profile {
                Manufacturer=manufacturer, Code=code, Material=material,
                Sku=sku, ManufacturerColour=manufacturerColour, Color=txtColor.Text.Trim(),
                NozzleMin=(int)nNozMin.Value, NozzleMax=(int)nNozMax.Value,
                BedMin=(int)nBedMin.Value, BedMax=(int)nBedMax.Value,
                Weight=ProtocolFilamentWeight(), Length=protocolLength, EmptySpool=(int)nEmptySpool.Value,
                NewGross=(int)nNewGross.Value, Verified=false
            };
        }

        void ValidateProfile(Profile p)
        {
            if(p.Code.Length>8) throw new Exception("Tag code can be at most 8 ASCII characters.");
            if(p.Material.Length>4) throw new Exception("Material can be at most 4 ASCII characters.");
            if(p.Sku.Length>12) throw new Exception("SKU can be at most 12 ASCII characters.");
            if(p.Weight<=0) throw new Exception("Weight must be greater than zero before writing a tag.");
            if(p.Length<=0) throw new Exception("Length must be greater than zero before writing a tag.");
            string h=p.Color.Replace("#","").ToUpperInvariant();
            if(h.Length!=6) throw new Exception("HEX colour must contain six digits after #, e.g. #000000.");
            int dummy; if(!int.TryParse(h, System.Globalization.NumberStyles.HexNumber, null, out dummy)) throw new Exception("Invalid HEX colour.");
        }

        void PickColor(object sender, EventArgs e)
        {
            using(var d=new ColorDialog()) {
                if(d.ShowDialog()==DialogResult.OK) txtColor.Text="#"+d.Color.R.ToString("X2")+d.Color.G.ToString("X2")+d.Color.B.ToString("X2");
            }
        }

        void RefreshReaders()
        {
            string previous=cbReader.SelectedItem==null ? "" : cbReader.SelectedItem.ToString();
            cbReader.Items.Clear();

            try {
                foreach(string reader in Pcsc.ListReaders()) cbReader.Items.Add(reader);

                if(!string.IsNullOrEmpty(previous) && cbReader.Items.Contains(previous))
                    cbReader.SelectedItem=previous;
                else if(cbReader.Items.Count>0)
                    cbReader.SelectedIndex=0;

                if(cbReader.Items.Count==0)
                    Log("No PC/SC reader found.");
                else
                    Log(cbReader.Items.Count+" reader(s) found.");
            }
            catch(Exception ex) {
                Log("Reader refresh failed: "+ex.Message);
            }
        }

        Pcsc.Connection Open()
        {
            if(cbReader.SelectedItem==null) throw new Exception("No reader selected.");
            var c=Pcsc.Open(cbReader.SelectedItem.ToString());
            lblUid.Text="UID: "+Pcsc.GetUid(c);
            return c;
        }


        bool ProfilesTagEquivalent(Profile a, Profile b)
        {
            if(a==null || b==null) return false;

            bool skuMatch = !string.IsNullOrWhiteSpace(a.Sku) && !string.IsNullOrWhiteSpace(b.Sku) &&
                            string.Equals(a.Sku.Trim(),b.Sku.Trim(),StringComparison.OrdinalIgnoreCase);
            bool codeMatch = !string.IsNullOrWhiteSpace(a.Code) && !string.IsNullOrWhiteSpace(b.Code) &&
                             string.Equals(a.Code.Trim(),b.Code.Trim(),StringComparison.OrdinalIgnoreCase);
            bool materialMatch = string.Equals((a.Material??"").Trim(),(b.Material??"").Trim(),StringComparison.OrdinalIgnoreCase);
            bool colourMatch = string.Equals((a.Color??"").Trim(),(b.Color??"").Trim(),StringComparison.OrdinalIgnoreCase);
            bool weightMatch = a.Weight==b.Weight;
            bool lengthMatch = a.Length==b.Length;

            if(skuMatch && materialMatch && colourMatch) return true;
            if(codeMatch && materialMatch && colourMatch && weightMatch && lengthMatch) return true;
            return materialMatch && colourMatch && weightMatch && lengthMatch;
        }

        Profile FindMatchingProfile(Profile tag)
        {
            Profile best=null;
            int bestScore=-1;

            foreach(var kv in profiles) {
                Profile p=kv.Value;
                bool equivalent=ProfilesTagEquivalent(p,tag);
                bool generatedIdentity=false;
                if(!string.IsNullOrWhiteSpace(p.Manufacturer) &&
                   !string.Equals(p.Manufacturer,"Anycubic",StringComparison.OrdinalIgnoreCase)) {
                    string gc=ProtocolCodeForManufacturer(p.Manufacturer);
                    string gs=ProtocolSkuForProfile(p.Manufacturer,p.Color);
                    generatedIdentity=Eq(gc,tag.Code) && Eq(gs,tag.Sku) &&
                                      Eq(p.Material,tag.Material) && Eq(p.Color,tag.Color);
                }
                if(!equivalent && !generatedIdentity) continue;

                int score=generatedIdentity ? 30 : 0;
                if(!string.IsNullOrWhiteSpace(p.Sku) && !string.IsNullOrWhiteSpace(tag.Sku) &&
                   string.Equals(p.Sku.Trim(),tag.Sku.Trim(),StringComparison.OrdinalIgnoreCase)) score+=10;
                if(!string.IsNullOrWhiteSpace(p.Code) && !string.IsNullOrWhiteSpace(tag.Code) &&
                   string.Equals(p.Code.Trim(),tag.Code.Trim(),StringComparison.OrdinalIgnoreCase)) score+=8;
                if(string.Equals((p.Material??"").Trim(),(tag.Material??"").Trim(),StringComparison.OrdinalIgnoreCase)) score+=4;
                if(string.Equals((p.Color??"").Trim(),(tag.Color??"").Trim(),StringComparison.OrdinalIgnoreCase)) score+=4;
                if(p.Weight==tag.Weight) score+=2;
                if(p.Length==tag.Length) score+=2;
                if(!string.IsNullOrWhiteSpace(p.Manufacturer)) score+=1;
                if(!string.IsNullOrWhiteSpace(p.ManufacturerColour)) score+=1;

                if(score>bestScore) {
                    bestScore=score;
                    best=p;
                }
            }
            return best;
        }


        string ManufacturerForTag(Profile tag, Profile matched)
        {
            if(tag!=null && string.Equals((tag.Code??"").Trim(),"AC",StringComparison.OrdinalIgnoreCase))
                return "Anycubic";
            if(matched!=null && !string.IsNullOrWhiteSpace(matched.Manufacturer))
                return matched.Manufacturer;

            string found="";
            foreach(var kv in profiles) {
                Profile q=kv.Value;
                if(q==null || string.IsNullOrWhiteSpace(q.Manufacturer)) continue;
                if(!string.IsNullOrWhiteSpace(tag.Code) &&
                   string.Equals((q.Code??"").Trim(),tag.Code.Trim(),StringComparison.OrdinalIgnoreCase)) {
                    if(found=="" || string.Equals(found,q.Manufacturer,StringComparison.OrdinalIgnoreCase))
                        found=q.Manufacturer;
                    else
                        return ""; // ambiguous code: do not guess
                }
            }
            return found;
        }

        string BuildDiagnostic()
        {
            var sb=new StringBuilder();
            sb.AppendLine("===== ACE TAG DIAGNOSTIC =====");
            sb.AppendLine("Reader:              "+(cbReader.SelectedItem==null ? "" : cbReader.SelectedItem.ToString()));
            sb.AppendLine("Profile:             "+txtProfileName.Text);
            sb.AppendLine("Manufacturer:        "+txtManufacturer.Text);
            sb.AppendLine("Manufacturer Colour: "+txtManufacturerColour.Text);
            sb.AppendLine();

            Pcsc.Connection c=null;
            try {
                c=Open();
                sb.AppendLine("UID:                 "+Pcsc.GetUid(c));
                byte[] ace=Ace.ReadArea(c);
                Profile tag=Ace.Decode(ace);
                sb.AppendLine("Tag Code:            "+tag.Code);
                sb.AppendLine("SKU:                 "+tag.Sku);
                sb.AppendLine("Material:            "+tag.Material);
                sb.AppendLine("Colour HEX:          "+tag.Color);
                sb.AppendLine("Nozzle C:            "+tag.NozzleMin+" to "+tag.NozzleMax);
                sb.AppendLine("Bed C:               "+tag.BedMin+" to "+tag.BedMax);
                sb.AppendLine("Weight g:            "+tag.Weight);
                sb.AppendLine("Length m (protocol): "+tag.Length);
                sb.AppendLine();

                sb.AppendLine("--- FULL ACCESSIBLE TAG DUMP ---");
                sb.AppendLine("(Physical NFC page numbers; read-only diagnostic)");
                int page=0;
                while(page<=0x86) {
                    try {
                        byte[] block=Pcsc.Read4(c,page);
                        int pagesInBlock=Math.Min(4,0x87-page);
                        for(int k=0;k<pagesInBlock;k++) {
                            sb.Append("Page "+(page+k).ToString("X2")+": ");
                            for(int j=0;j<4;j++) {
                                if(j>0) sb.Append(" ");
                                sb.Append(block[k*4+j].ToString("X2"));
                            }
                            sb.AppendLine();
                        }
                        page+=4;
                    } catch(Exception ex) {
                        sb.AppendLine("Page "+page.ToString("X2")+": READ STOP ("+ex.Message+")");
                        break;
                    }
                }

                sb.AppendLine();
                sb.AppendLine("--- ACE WRITABLE AREA 04-27 ---");
                for(int off=0;off<ace.Length;off+=4) {
                    int physicalPage=0x04+(off/4);
                    sb.Append("Page "+physicalPage.ToString("X2")+": ");
                    for(int j=0;j<4;j++) {
                        if(j>0) sb.Append(" ");
                        sb.Append(ace[off+j].ToString("X2"));
                    }
                    sb.AppendLine();
                }
            } catch(Exception ex) {
                sb.AppendLine("DIAGNOSTIC ERROR: "+ex.Message);
            } finally { if(c!=null)c.Dispose(); }
            sb.AppendLine("==============================");
            return sb.ToString();
        }

        void CopyDiagnostic(object sender, EventArgs e)
        {
            try {
                Clipboard.SetText(BuildDiagnostic());
                Log("Diagnostic copied to clipboard.");
            } catch(Exception ex) {
                Log("Diagnostic failed: "+ex.Message);
                MessageBox.Show(ex.Message,"Diagnostic failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
            }
        }

        void ReadTag(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                c=Open();
                byte[] all=Ace.ReadArea(c);
                var p=Ace.Decode(all);
                bool blank=Ace.IsBlank(all);
                Profile matchedProfile = blank ? null : FindMatchingProfile(p);
                lblStatus.Text=blank ? "Tag status: BLANK / unprogrammed" : "Tag status: PROGRAMMED";
                Log(blank ? "Read OK: blank tag." : "Read OK: "+p.Material+" "+p.Color+" | SKU "+p.Sku+" | Code "+p.Code+" | "+p.Weight+" g | "+p.Length+" m");

                loadingProfile=true;
                lbProfiles.ClearSelected();
                txtProfileName.Text="";
                txtManufacturer.Text=ManufacturerForTag(p,matchedProfile);
                txtManufacturerColour.Text=matchedProfile==null ? "" : matchedProfile.ManufacturerColour;
                txtCode.Text=p.Code; txtMaterial.Text=p.Material; txtSku.Text=p.Sku;
                txtColor.Text=string.IsNullOrWhiteSpace(p.Color) ? "#" : p.Color;
                loadingProfile=false;
                if(blank) {
                    loadingProfile=true;
                    txtCode.Text=""; txtMaterial.Text=""; txtSku.Text=""; txtManufacturerColour.Text=""; txtColor.Text="#";
                    pnlColourSwatch.BackColor=SystemColors.Control;
                    nNozMin.Value=0; nNozMax.Value=0; nBedMin.Value=0; nBedMax.Value=0;
                    nWeight.Value=0; nLength.Value=330;
                    loadingProfile=false;
                } else {
                    nNozMin.Value=p.NozzleMin; nNozMax.Value=p.NozzleMax; nBedMin.Value=p.BedMin; nBedMax.Value=p.BedMax;
                    nWeight.Value=Math.Min(nWeight.Maximum,Math.Max(nWeight.Minimum,p.Weight));
                    int readLength = p.Length > 0 ? p.Length : 330;
                    nLength.Value=Math.Min(nLength.Maximum,Math.Max(nLength.Minimum,readLength));
                            HexChanged();
                }
                UpdateGeneratedProfileName();
                UpdateWeightCheck();
                UpdateFumeWarning();
                dirty=false;
            } catch(Exception ex){ Log("READ FAILED: "+ex.Message); MessageBox.Show(ex.Message,"Read failed"); }
            finally { if(c!=null) c.Dispose(); }
        }

        void SetLength330Only(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                c=Open();
                byte[] before=Ace.ReadArea(c);
                var existing=Ace.Decode(before);

                if(MessageBox.Show(
                    "CONTROLLED LENGTH TEST ONLY\r\n\r\n"+
                    "Current protocol length: "+existing.Length+" m\r\n\r\n"+
                    "This changes ONLY physical page 1E to:\r\n"+
                    "AF 00 4A 01  (1.75 mm / 330 m)\r\n\r\n"+
                    "Every other ACE writable byte must remain unchanged.\r\n\r\nContinue?",
                    "Set Length 330m",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK) return;

                Pcsc.WritePage(c,0x1E,new byte[]{0xAF,0x00,0x4A,0x01});

                byte[] after=Ace.ReadArea(c);
                var got=Ace.Decode(after);
                if(got.Length!=330)
                    throw new Exception("Length read-back verification failed. Tag reports "+got.Length+" m.");

                for(int i=0;i<before.Length && i<after.Length;i++) {
                    int physicalPage=0x04+(i/4);
                    if(physicalPage==0x1E) continue;
                    if(before[i]!=after[i])
                        throw new Exception("Unexpected change outside page 1E at page "+physicalPage.ToString("X2")+".");
                }

                int off=(0x1E-0x04)*4;
                if(after[off]!=0xAF || after[off+1]!=0x00 || after[off+2]!=0x4A || after[off+3]!=0x01)
                    throw new Exception("Physical page 1E did not verify as AF 00 4A 01.");

                Log("LENGTH-ONLY TEST WRITE + VERIFY SUCCESS: "+existing.Length+" m -> 330 m; page 1E = AF 00 4A 01; all other ACE writable bytes unchanged.");
                MessageBox.Show(
                    "Length changed and physically verified.\r\n\r\n"+
                    "Page 1E = AF 00 4A 01 (330 m).\r\n\r\n"+
                    "Now put the tag in the ACE and note exactly what it reports.",
                    "Length test ready",MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(Exception ex) {
                Log("LENGTH-ONLY TEST FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"Length test failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
            } finally { if(c!=null)c.Dispose(); }
        }

        void SetCodeAcOnly(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                c=Open();
                byte[] before=Ace.ReadArea(c);
                var existing=Ace.Decode(before);

                if(MessageBox.Show(
                    "CONTROLLED TAG CODE TEST ONLY\r\n\r\n"+
                    "Current physical Tag Code: "+existing.Code+"\r\n\r\n"+
                    "This changes ONLY physical page 0A to:\r\n"+
                    "41 43 00 00  (AC)\r\n\r\n"+
                    "Every other ACE writable byte must remain unchanged.\r\n\r\nContinue?",
                    "Set Code AC",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK) return;

                Pcsc.WritePage(c,0x0A,new byte[]{0x41,0x43,0x00,0x00});

                byte[] after=Ace.ReadArea(c);
                var got=Ace.Decode(after);
                if(!Eq(got.Code,"AC"))
                    throw new Exception("Code read-back verification failed. Tag reports '"+got.Code+"'.");

                for(int i=0;i<before.Length && i<after.Length;i++) {
                    int physicalPage=0x04+(i/4);
                    if(physicalPage==0x0A) continue;
                    if(before[i]!=after[i])
                        throw new Exception("Unexpected change outside page 0A at page "+physicalPage.ToString("X2")+".");
                }

                int off=(0x0A-0x04)*4;
                if(after[off]!=0x41 || after[off+1]!=0x43 || after[off+2]!=0x00 || after[off+3]!=0x00)
                    throw new Exception("Physical page 0A did not verify as 41 43 00 00.");

                Log("CODE-ONLY TEST WRITE + VERIFY SUCCESS: "+existing.Code+" -> AC; page 0A = 41 43 00 00; all other ACE writable bytes unchanged.");
                MessageBox.Show(
                    "Tag Code changed and physically verified.\r\n\r\n"+
                    "Page 0A = 41 43 00 00 (AC).\r\n\r\n"+
                    "Now click Read Tag, then Copy Diagnostic.",
                    "Code test ready",MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(Exception ex) {
                Log("CODE-ONLY TEST FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"Code test failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
            } finally { if(c!=null)c.Dispose(); }
        }

        void SetGreySkuOnly(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                c=Open();
                byte[] before=Ace.ReadArea(c);
                var existing=Ace.Decode(before);

                if(MessageBox.Show(
                    "CONTROLLED SKU TEST ONLY\r\n\r\n"+
                    "Current tag:\r\n"+
                    "SKU: "+existing.Sku+"\r\n"+
                    "Code: "+existing.Code+"\r\n"+
                    "Material: "+existing.Material+"\r\n"+
                    "Colour: "+existing.Color+"\r\n\r\n"+
                    "This changes ONLY the hidden SKU bytes to:\r\n"+
                    "AHPLGY-107\r\n\r\n"+
                    "All other ACE writable bytes remain untouched.\r\n\r\nContinue?",
                    "Set SKU AHPLGY-107",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK) return;

                // SKU occupies physical pages 05-07 (12 ASCII bytes).
                byte[] sku=new byte[12];
                byte[] ascii=Encoding.ASCII.GetBytes("AHPLGY-107");
                Array.Copy(ascii,sku,ascii.Length);
                for(int i=0;i<3;i++) {
                    byte[] page=new byte[4];
                    Array.Copy(sku,i*4,page,0,4);
                    Pcsc.WritePage(c,0x05+i,page);
                }

                byte[] after=Ace.ReadArea(c);
                var got=Ace.Decode(after);
                if(!Eq(got.Sku,"AHPLGY-107"))
                    throw new Exception("SKU read-back verification failed. Tag reports '"+got.Sku+"'.");

                // Prove no ACE writable byte outside pages 05-07 changed.
                for(int i=0;i<before.Length && i<after.Length;i++) {
                    int physicalPage=0x04+(i/4);
                    if(physicalPage>=0x05 && physicalPage<=0x07) continue;
                    if(before[i]!=after[i])
                        throw new Exception("Unexpected change outside SKU at page "+physicalPage.ToString("X2")+".");
                }

                Log("SKU-ONLY TEST WRITE + VERIFY SUCCESS: "+existing.Sku+" -> AHPLGY-107; all other ACE writable bytes unchanged.");
                MessageBox.Show(
                    "SKU changed and verified.\r\n\r\nOnly pages 05-07 were changed.\r\n"+
                    "Now put the tag in the ACE and note exactly what it reports.",
                    "SKU test ready",MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(Exception ex) {
                Log("SKU-ONLY TEST FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"SKU test failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
            } finally { if(c!=null)c.Dispose(); }
        }

        void WriteGenuineGreyTest(object sender, EventArgs e)
        {
            Profile grey=new Profile {
                Manufacturer="Anycubic", ManufacturerColour="Grey", Code="AC", Material="PLA",
                Sku="AHPLGY-107", Color="#75787B",
                NozzleMin=190, NozzleMax=230, BedMin=55, BedMax=65,
                Weight=1000, Length=330
            };

            if(MessageBox.Show(
                "GREY CLONE TEST ONLY\r\n\r\n"+
                "Writes the known genuine Anycubic Grey PLA writable payload:\r\n"+
                "AHPLGY-107 | AC | PLA | #7B7875\r\n"+
                "190-230 C nozzle | 55-65 C bed | 330 m | 1000 g\r\n\r\n"+
                "UID/manufacturer pages are NOT copied.\r\n\r\nContinue?",
                "Write Genuine Grey Test",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK) return;

            Pcsc.Connection c=null;
            try {
                c=Open();
                byte[] before=Ace.ReadArea(c);
                if(!Ace.IsBlank(before) &&
                   MessageBox.Show("This tag already appears programmed.\r\n\r\nOverwrite its writable ACE data with the Grey test payload?",
                                   "Overwrite tag?",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;

                var map=Ace.BuildMap(grey);
                foreach(var kv in map) Pcsc.WritePage(c,kv.Key,kv.Value);

                byte[] after=Ace.ReadArea(c);
                byte[] expected=Ace.MapToBytes(map);
                if(!Ace.BytesEqual(after,expected))
                    throw new Exception("Read-back verification did not exactly match the intended Grey payload. "+Ace.FirstMismatch(after,expected));

                Log("GREY CLONE TEST WRITE + VERIFY SUCCESS: AHPLGY-107 | AC | PLA | #7B7875 | 330 m | 1000 g");
                MessageBox.Show("Grey clone written and verified successfully.\r\n\r\nNow click Copy Diagnostic before putting it in the ACE.",
                                "Grey test ready",MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch(Exception ex) {
                Log("GREY CLONE TEST FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"Grey test failed",MessageBoxButtons.OK,MessageBoxIcon.Error);
            } finally { if(c!=null)c.Dispose(); }
        }

        void WriteTag(object sender, EventArgs e)
        {
            try {
                string profileName=GeneratedProfileName();
                if(dirty || profileName=="" || FindProfileKeyCI(profileName)=="") {
                    MessageBox.Show("This profile has not been saved, or has unsaved changes.\r\n\r\nSave Profile before writing the tag.",
                                    "Save profile first",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                    Log("WRITE BLOCKED: profile must be saved before writing.");
                    return;
                }
                var p=Current(); ValidateProfile(p);
                int qty=(int)nWriteQty.Value;

                for(int item=1; item<=qty; item++) {
                    if(item>1) {
                        if(MessageBox.Show("Remove the previous tag and place tag "+item+" of "+qty+" on the reader.\r\n\r\nClick OK when ready.",
                                           "Next tag",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK) {
                            Log("BATCH WRITE CANCELLED."); break;
                        }
                    }

                    Pcsc.Connection c=null;
                    try {
                        c=Open();
                        byte[] before=Ace.ReadArea(c);
                        if(!Ace.IsBlank(before)) {
                            var existing=Ace.Decode(before);
                            string msg="This tag already appears programmed.\r\n\r\n"+
                                       "Existing: "+existing.Material+" "+existing.Color+" | SKU "+existing.Sku+" | Code "+existing.Code+"\r\n\r\n"+
                                       "Overwrite it?";
                            if(MessageBox.Show(msg,"Overwrite programmed tag?",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) {
                                Log("WRITE SKIPPED: programmed tag overwrite declined.");
                                continue;
                            }
                        }

                        var map=Ace.BuildMap(p);
                        foreach(var kv in map) Pcsc.WritePage(c,kv.Key,kv.Value);
                        byte[] got=Ace.ReadArea(c);
                        byte[] expected=Ace.MapToBytes(map);

                        if(!Ace.BytesEqual(got,expected))
                            throw new Exception("Read-back verification did not match what was written. "+Ace.FirstMismatch(got,expected));

                        lblStatus.Text="Tag status: VERIFIED";
                        string writtenUid=Pcsc.GetUid(c);
                        Log("WRITE + VERIFY SUCCESS. UID "+writtenUid+" ("+item+"/"+qty+")");
                        RecordReel(writtenUid,p);

                        MessageBox.Show("Tag "+item+" of "+qty+" written and verified successfully.",
                                        "Success",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    } finally { if(c!=null) c.Dispose(); }
                }
            } catch(Exception ex) {
                Log("WRITE FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"Write failed");
            }
        }

        void VerifyTag(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                var wanted=Current(); ValidateProfile(wanted);
                c=Open();
                var gotBytes=Ace.ReadArea(c);
                var got=Ace.Decode(gotBytes);

                var diffs = new List<string>();
                if(!Eq(got.Code,wanted.Code)) diffs.Add("Code: tag '"+got.Code+"' / screen '"+wanted.Code+"'");
                if(!Eq(got.Material,wanted.Material)) diffs.Add("Material: tag '"+got.Material+"' / screen '"+wanted.Material+"'");
                if(!Eq(got.Sku,wanted.Sku)) diffs.Add("SKU: tag '"+got.Sku+"' / screen '"+wanted.Sku+"'");
                if(!Eq(got.Color,wanted.Color)) diffs.Add("Colour: tag '"+got.Color+"' / screen '"+wanted.Color+"'");
                if(got.NozzleMin!=wanted.NozzleMin || got.NozzleMax!=wanted.NozzleMax) diffs.Add("Nozzle: tag "+got.NozzleMin+"-"+got.NozzleMax+" / screen "+wanted.NozzleMin+"-"+wanted.NozzleMax);
                if(got.BedMin!=wanted.BedMin || got.BedMax!=wanted.BedMax) diffs.Add("Bed: tag "+got.BedMin+"-"+got.BedMax+" / screen "+wanted.BedMin+"-"+wanted.BedMax);
                if(got.Weight!=wanted.Weight) diffs.Add("Weight: tag "+got.Weight+" / screen "+wanted.Weight);
                if(got.Length!=wanted.Length) diffs.Add("Length: tag "+got.Length+" / screen "+wanted.Length);

                if(diffs.Count>0) {
                    string msg="Tag fields differ:\r\n\r\n"+string.Join("\r\n",diffs.ToArray());
                    Log("VERIFY FAILED: "+string.Join(" | ",diffs.ToArray()));
                    MessageBox.Show(msg,"Verify failed");
                    return;
                }

                lblStatus.Text="Tag status: VERIFIED";
                Log("VERIFY SUCCESS (decoded ACE fields match).");
                MessageBox.Show("Tag matches the ACE fields on screen.","Verified");
            } catch(Exception ex){ Log("VERIFY FAILED: "+ex.Message); MessageBox.Show(ex.Message,"Verify failed"); }
            finally { if(c!=null) c.Dispose(); }
        }

        static bool Eq(string a,string b)
        {
            return string.Equals((a??"").Trim(),(b??"").Trim(),StringComparison.OrdinalIgnoreCase);
        }

        string FindProfileKeyCI(string name)
        {
            foreach(string key in profiles.Keys)
                if(string.Equals(key,name,StringComparison.OrdinalIgnoreCase)) return key;
            return "";
        }

        void SaveProfile(object sender, EventArgs e)
        {
            try {
                var p=Current(); ValidateProfile(p);
                string name=GeneratedProfileName();
                if(name=="") throw new Exception("Enter Manufacturer, Manufacturer Colour and/or Material before saving.");

                // If a saved profile was loaded, Save means UPDATE THAT RECORD even if
                // identity text (for example SUNLU -> Sunlu) has been edited.
                string oldKey=editingProfileKey;
                string collisionKey=FindProfileKeyCI(name);
                bool editingExisting=oldKey!="" && FindProfileKeyCI(oldKey)!="";
                bool collisionIsOther=collisionKey!="" && (!editingExisting || !string.Equals(collisionKey,oldKey,StringComparison.OrdinalIgnoreCase));

                if(collisionIsOther) {
                    var answer=MessageBox.Show("Another profile named "+collisionKey+" already exists.\r\n\r\nReplace that profile with these values?",
                                               "Existing profile",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
                    if(answer!=DialogResult.Yes) return;
                } else if(editingExisting) {
                    var answer=MessageBox.Show("Update the loaded profile with these values?\r\n\r\n"+oldKey+"  →  "+name,
                                               "Update profile",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
                    if(answer!=DialogResult.Yes) return;
                } else if(collisionKey!="") {
                    var answer=MessageBox.Show("A profile named "+collisionKey+" already exists.\r\n\r\nUpdate the existing profile with these values?",
                                               "Existing profile",MessageBoxButtons.YesNo,MessageBoxIcon.Question);
                    if(answer!=DialogResult.Yes) return;
                    oldKey=collisionKey; editingExisting=true;
                }

                string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ACEPro2TagWriter");
                Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,"profiles.tsv");
                var rows=new List<string>();
                if(File.Exists(path)) rows.AddRange(File.ReadAllLines(path));
                string line=string.Join("\t",new string[]{name,p.Manufacturer,p.Code,p.Material,p.Sku,p.Color,p.NozzleMin.ToString(),p.NozzleMax.ToString(),p.BedMin.ToString(),p.BedMax.ToString(),p.Weight.ToString(),p.Length.ToString(),p.EmptySpool.ToString(),p.NewGross.ToString(),"0",p.ManufacturerColour});

                bool replaced=false;
                string targetKey=editingExisting ? oldKey : (collisionKey!="" ? collisionKey : name);
                for(int i=0;i<rows.Count;i++) {
                    var parts=rows[i].Split('\t');
                    if(parts.Length>0 && string.Equals(parts[0],targetKey,StringComparison.OrdinalIgnoreCase)) {
                        rows[i]=line; replaced=true; break;
                    }
                }
                if(!replaced) rows.Add(line);
                File.WriteAllLines(path,rows.ToArray());

                // Remove the old in-memory key before adding the edited name.
                string memOld=FindProfileKeyCI(targetKey);
                if(memOld!="" && !string.Equals(memOld,name,StringComparison.Ordinal)) profiles.Remove(memOld);
                string memCollision=FindProfileKeyCI(name);
                if(memCollision!="" && !string.Equals(memCollision,name,StringComparison.Ordinal)) profiles.Remove(memCollision);
                profiles[name]=p;
                editingProfileKey=name;
                Log((editingExisting ? "Profile updated: " : "Profile saved: ")+name);
                dirty=false;
                LoadSavedProfiles();
                RefreshProfileList();
                loadingProfile=true;
                for(int i=0;i<lbProfiles.Items.Count;i++) {
                    if(string.Equals(lbProfiles.Items[i].ToString(),name,StringComparison.OrdinalIgnoreCase)) { lbProfiles.SelectedIndex=i; break; }
                }
                loadingProfile=false;
                editingProfileKey=name;
                dirty=false;
            } catch(Exception ex){ Log("SAVE FAILED: "+ex.Message); MessageBox.Show(ex.Message,"Save profile failed"); }
        }

        void LoadSavedProfiles()
        {
            try {
                string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ACEPro2TagWriter");
                string path=Path.Combine(dir,"profiles.tsv");
                if(File.Exists(path)) {
                    foreach(string row in File.ReadAllLines(path)) {
                        if(string.IsNullOrWhiteSpace(row)) continue;
                        var x=row.Split('\t');
                        if(x.Length<12) continue;
                        int a,b,c,d,w,l,empty=0,newgross=0;
                        if(!int.TryParse(x[6],out a) || !int.TryParse(x[7],out b) || !int.TryParse(x[8],out c) || !int.TryParse(x[9],out d) || !int.TryParse(x[10],out w) || !int.TryParse(x[11],out l)) continue;
                        if(x.Length>=13) int.TryParse(x[12],out empty);
                        if(x.Length>=14) int.TryParse(x[13],out newgross);
                        bool verified=false; // legacy column ignored: Tested flag retired in v1.3
                        string manufacturerColour=x.Length>=16 ? x[15] : "";
                        profiles[x[0]]=new Profile{Manufacturer=x[1],Code=x[2],Material=x[3],Sku=x[4],Color=x[5],ManufacturerColour=manufacturerColour,NozzleMin=a,NozzleMax=b,BedMin=c,BedMax=d,Weight=w,Length=l,EmptySpool=empty,NewGross=newgross,Verified=verified};
                    }
                }
                RefreshChoiceListsFromProfiles();
            } catch(Exception ex){ Log("PROFILE LOAD FAILED: "+ex.Message); }
        }





        void ClearFilamentFields()
        {
            loadingProfile=true;
            txtProfileName.Text="";
            txtManufacturer.Text="";
            txtCode.Text="";
            txtMaterial.Text="";
            lblFumeWarning.Text="";
            txtSku.Text="";
            txtManufacturerColour.Text="";
            txtColor.Text="#";
            pnlColourSwatch.BackColor=SystemColors.Control;

            nNozMin.Value=0; nNozMax.Value=0;
            nBedMin.Value=0; nBedMax.Value=0;
            nWeight.Value=1000; nLength.Value=330;
            nNewGross.Value=1200; nEmptySpool.Value=0; nCurrentGross.Value=0;
            nPrintNeed.Value=0; nSafetyMargin.Value=50; nWriteQty.Value=1;

            lblUid.Text="UID: —";
            lblStatus.Text="Tag status: —";
            lblRemaining.Text="Remaining: —";
            lblPrintCheck.Text="Print check: —";
            Control[] found=Controls.Find("lblClosestColour",true);
            if(found.Length>0) found[0].Text="";
            Control[] existing=Controls.Find("lblExistingProfile",true); if(existing.Length>0) existing[0].Text="";

            loadingProfile=false;
            editingProfileKey="";
            dirty=false;
        }

        void WireDirtyTracking()
        {
            foreach(Control c in Controls) WireDirtyControl(c);

            txtManufacturer.TextChanged += (s,e)=>IdentityFieldChanged();
            txtManufacturerColour.TextChanged += (s,e)=>IdentityFieldChanged();
            txtMaterial.TextChanged += (s,e)=>IdentityFieldChanged();
        }

        void WireDirtyControl(Control c)
        {
            if(c==txtLog || c==txtProfileName || c==lbProfiles || c==cbReader) {
                // status/navigation controls do not make a profile dirty
            }
            else if(c is TextBox) ((TextBox)c).TextChanged += (s,e)=>MarkDirty();
            else if(c is NumericUpDown) ((NumericUpDown)c).ValueChanged += (s,e)=>MarkDirty();
            else if(c is ComboBox) {
                ((ComboBox)c).TextChanged += (s,e)=>MarkDirty();
                ((ComboBox)c).SelectedIndexChanged += (s,e)=>MarkDirty();
            }
            foreach(Control child in c.Controls) WireDirtyControl(child);
        }

        void IdentityFieldChanged()
        {
            UpdateGeneratedProfileName();
            UpdateExistingProfileWarning();
            if(!loadingProfile) dirty=true;
        }

        void UpdateExistingProfileWarning()
        {
            Control[] found=Controls.Find("lblExistingProfile",true);
            if(found.Length==0) return;
            string name=GeneratedProfileName();
            string selected=SelectedProfileKey();
            string foundKey=FindProfileKeyCI(name);
            bool exists=name!="" && foundKey!="" && !string.Equals(foundKey,selected,StringComparison.OrdinalIgnoreCase);
            found[0].Text=exists ? "Existing profile found — select it from the profile list to load it." : "";
        }

        void MarkDirty()
        {
            if(!loadingProfile) dirty=true;
        }

        void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if(!dirty) return;
            var r=MessageBox.Show("You have unsaved profile changes.\r\n\r\nSave changes before closing?",
                                  "Save changes?",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
            if(r==DialogResult.Cancel){ e.Cancel=true; return; }
            if(r==DialogResult.Yes){ SaveProfile(null,EventArgs.Empty); dirty=false; }
        }


        void ProfileListMouseDown(object sender, MouseEventArgs e)
        {
            if(e.Button!=MouseButtons.Right) return;
            int index=lbProfiles.IndexFromPoint(e.Location);
            if(index==ListBox.NoMatches) return;

            loadingProfile=true;
            lbProfiles.SelectedIndex=index;
            loadingProfile=false;

            string display=lbProfiles.Items[index].ToString();
            var menu=new ContextMenuStrip();
            var deleteItem=new ToolStripMenuItem("Delete "+display);
            deleteItem.Click += (s,ev)=>DeleteProfileByName(display);
            menu.Items.Add(deleteItem);
            menu.Show(lbProfiles,e.Location);
        }

        void DeleteProfileRow(string path,string name)
        {
            if(!File.Exists(path)) return;
            var kept=new List<string>();
            foreach(string row in File.ReadAllLines(path)) {
                string[] parts=row.Split('\t');
                if(parts.Length>0 && string.Equals(parts[0],name,StringComparison.OrdinalIgnoreCase)) continue;
                kept.Add(row);
            }
            File.WriteAllLines(path,kept.ToArray());
        }

        void DeleteProfileByName(string display)
        {
            string name=display.StartsWith("✓ ") ? display.Substring(2) : display;
            if(MessageBox.Show("Delete this saved profile?\r\n\r\n"+name+"\r\n\r\nARE YOU SURE?",
                               "Delete saved profile",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes) return;
            string path=Path.Combine(DataDir(),"profiles.tsv");
            DeleteProfileRow(path,name);
            // Legacy GitHub imports live in a separate local cache. Update-from-GitHub
            // was removed, so deletion must remove the cached row too.
            DeleteProfileRow(Path.Combine(DataDir(),"github_profiles.tsv"),name);
            profiles.Remove(name);
            RefreshProfileList();
            Log("Deleted saved profile: "+name);
        }

        void CloseProfile(object sender, EventArgs e)
        {
            if(dirty) {
                var r=MessageBox.Show("This profile has unsaved changes.\r\n\r\nSave changes before closing it?",
                                      "Close profile",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(r==DialogResult.Cancel) return;
                if(r==DialogResult.Yes) SaveProfile(null,EventArgs.Empty);
            }
            lbProfiles.ClearSelected();
            ClearFilamentFields();
            Log("Profile closed.");
        }

        void UpdateFumeWarning()
        {
            string m=(txtMaterial.Text ?? "").Trim().ToUpperInvariant();
            bool caution =
                m=="ABS" || m.StartsWith("ABS ") || m.StartsWith("ABS-") ||
                m=="ASA" || m.StartsWith("ASA ") || m.StartsWith("ASA-") ||
                m.Contains("NYLON") || m=="PA" || m.StartsWith("PA ") || m.StartsWith("PA-") ||
                m=="PC" || m.StartsWith("PC ") || m.StartsWith("PC-");
            lblFumeWarning.Text=caution
                ? "FUME CAUTION — Use suitable ventilation/extraction and follow the filament manufacturer's safety guidance."
                : "";
        }

        void DeleteProfile(object sender, EventArgs e)
        {
            DeleteSelectedProfile();
        }

        void DeleteSelectedProfile()
        {
            if(lbProfiles.SelectedItem==null) {
                MessageBox.Show("Select a profile first.","Delete profile");
                return;
            }
            DeleteProfileByName(lbProfiles.SelectedItem.ToString());
        }

        void SelectColourNameFromHex(string hex)
        {
            var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                {"Black","#000000"},{"White","#FFFFFF"},{"Grey","#808080"},{"Red","#FF0000"},
                {"Orange","#FFA500"},{"Yellow","#FFFF00"},{"Green","#008000"},{"Blue","#0000FF"},
                {"Purple","#800080"},{"Pink","#FFC0CB"},{"Brown","#A52A2A"},{"Beige","#F5F5DC"},
                {"Silver","#C0C0C0"},{"Gold","#FFD700"}
            };
            foreach(var kv in map) if(string.Equals(kv.Value,hex,StringComparison.OrdinalIgnoreCase)) {
                cbColourName.SelectedItem=kv.Key; return;
            }
            cbColourName.SelectedItem="Custom";
        }

        void ColourNameChanged()
        {
            if(cbColourName.SelectedItem==null) return;
            string n=cbColourName.SelectedItem.ToString();
            var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
                {"Black","#000000"},{"White","#FFFFFF"},{"Grey","#808080"},{"Red","#FF0000"},
                {"Orange","#FFA500"},{"Yellow","#FFFF00"},{"Green","#008000"},{"Blue","#0000FF"},
                {"Purple","#800080"},{"Pink","#FFC0CB"},{"Brown","#A52A2A"},{"Beige","#F5F5DC"},
                {"Silver","#C0C0C0"},{"Gold","#FFD700"}
            };
            string hex; if(map.TryGetValue(n,out hex)) txtColor.Text=hex;
        }

        void HexChanged()
        {
            string raw=txtColor.Text.Trim();
            if(raw=="" || raw=="#") {
                pnlColourSwatch.BackColor=SystemColors.Control;
                Control[] none=Controls.Find("lblClosestColour",true);
                if(none.Length>0) none[0].Text="";
                return;
            }

            string h=raw.Replace("#","");
            if(h.Length!=6) { pnlColourSwatch.BackColor=SystemColors.Control; return; }

            int val;
            if(!int.TryParse(h,System.Globalization.NumberStyles.HexNumber,null,out val)) {
                pnlColourSwatch.BackColor=SystemColors.Control; return;
            }

            int r=(val>>16)&255,g=(val>>8)&255,b=val&255;
            pnlColourSwatch.BackColor=Color.FromArgb(r,g,b);

            var named=new Dictionary<string,string> {
                {"Black","#000000"},{"White","#FFFFFF"},{"Grey","#808080"},{"Red","#FF0000"},
                {"Orange","#FFA500"},{"Yellow","#FFFF00"},{"Green","#008000"},{"Blue","#0000FF"},
                {"Purple","#800080"},{"Pink","#FFC0CB"},{"Brown","#A52A2A"},{"Beige","#F5F5DC"},
                {"Silver","#C0C0C0"},{"Gold","#FFD700"}
            };

            string best="Custom"; double bestD=1e30;
            foreach(var kv in named) {
                string x=kv.Value.Substring(1);
                int rr=Convert.ToInt32(x.Substring(0,2),16);
                int gg=Convert.ToInt32(x.Substring(2,2),16);
                int bb=Convert.ToInt32(x.Substring(4,2),16);
                double d=(r-rr)*(r-rr)+(g-gg)*(g-gg)+(b-bb)*(b-bb);
                if(d<bestD){bestD=d;best=kv.Key;}
            }
            Control[] found=Controls.Find("lblClosestColour",true);
            if(found.Length>0) found[0].Text="Closest: "+best;
        }

        string DataDir()
        {
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ACEPro2TagWriter");
            Directory.CreateDirectory(dir);
            return dir;
        }

        void RecordReel(string uid, Profile p)
        {
            try {
                string path=Path.Combine(DataDir(),"reels.csv");
                bool fresh=!File.Exists(path);
                using(var sw=new StreamWriter(path,true,Encoding.UTF8)) {
                    if(fresh) sw.WriteLine("Date,UID,Profile,Manufacturer,Code,Material,SKU,ManufacturerColour,ColourHEX,Weight_g,Length_m,NewReelGross_g,EmptySpool_g,Verified");
                    sw.WriteLine(Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))+","+
                                 Csv(uid)+","+Csv(txtProfileName.Text.Trim())+","+Csv(p.Manufacturer)+","+Csv(p.Code)+","+
                                 Csv(p.Material)+","+Csv(p.Sku)+","+Csv(p.ManufacturerColour)+","+Csv(p.Color)+","+p.Weight+","+p.Length+","+p.NewGross+","+p.EmptySpool+","+
                                 (p.Verified ? "Yes" : "No"));
                }
                Log("Reel record saved for UID "+uid);
            } catch(Exception ex){ Log("REEL RECORD FAILED: "+ex.Message); }
        }

        string Csv(string s)
        {
            if(s==null) s="";
            return "\""+s.Replace("\"","\"\"")+"\"";
        }

        void ShowReelRecords(object sender, EventArgs e)
        {
            try {
                string path=Path.Combine(DataDir(),"reels.csv");
                if(!File.Exists(path)) { MessageBox.Show("No reel records yet.","Reel Records"); return; }
                string[] lines=File.ReadAllLines(path);
                int count=Math.Max(0,lines.Length-1);
                string preview="";
                for(int i=Math.Max(1,lines.Length-8);i<lines.Length;i++) preview+=lines[i]+"\r\n";
                MessageBox.Show("Recorded reels: "+count+"\r\n\r\nMost recent:\r\n"+preview+
                                "\r\nFile:\r\n"+path,"Reel Records");
            } catch(Exception ex){ MessageBox.Show(ex.Message,"Reel Records"); }
        }

        void ExportReelsCsv(object sender, EventArgs e)
        {
            try {
                string src=Path.Combine(DataDir(),"reels.csv");
                if(!File.Exists(src)) { MessageBox.Show("No reel records to export.","Export"); return; }
                using(var d=new SaveFileDialog()) {
                    d.Filter="CSV files (*.csv)|*.csv";
                    d.FileName="ACE_Pro_2_Reel_Records.csv";
                    if(d.ShowDialog()!=DialogResult.OK) return;
                    File.Copy(src,d.FileName,true);
                    Log("Reel records exported to "+d.FileName);
                }
            } catch(Exception ex){ MessageBox.Show(ex.Message,"Export failed"); }
        }

        void ExportForGitHub(object sender, EventArgs e)
        {
            try {
                Profile p=Current(); ValidateProfile(p);
                string name=GeneratedProfileName();
                if(name=="") throw new Exception("Enter Manufacturer, Manufacturer Colour and/or Material before saving.");

                using(var d=new SaveFileDialog()) {
                    d.Filter="Markdown files (*.md)|*.md|Text files (*.txt)|*.txt";
                    d.FileName=SafeFileName(name)+"_GitHub.md";
                    if(d.ShowDialog()!=DialogResult.OK) return;

                    string md=
"# ACE Pro 2 filament profile contribution\r\n\r\n"+
"- Profile: "+name+"\r\n"+
"- Manufacturer: "+p.Manufacturer+"\r\n"+
"- Tag code: "+p.Code+"\r\n"+
"- Material: "+p.Material+"\r\n"+
"- SKU: "+p.Sku+"\r\n"+
"- Manufacturer colour name: "+(string.IsNullOrWhiteSpace(p.ManufacturerColour) ? "not supplied" : p.ManufacturerColour)+"\r\n"+
"- Colour HEX: "+p.Color+"\r\n"+
"- Nozzle: "+p.NozzleMin+"-"+p.NozzleMax+" C\r\n"+
"- Bed: "+p.BedMin+"-"+p.BedMax+" C\r\n"+
"- Nominal filament: "+p.Weight+" g / "+p.Length+" m\r\n"+
"- New reel gross weight: "+(p.NewGross>0 ? p.NewGross+" g" : "not measured")+"\r\n"+
"- Empty spool weight: "+(p.EmptySpool>0 ? p.EmptySpool+" g" : "not measured")+"\r\n"+
"- Physically verified: "+(p.Verified ? "YES" : "NO")+"\r\n\r\n"+
"## ACE RFID fields\r\n\r\n"+
"```text\r\n"+
"SKU="+p.Sku+"\r\n"+
"VENDOR="+p.Code+"\r\n"+
"MATERIAL="+p.Material+"\r\n"+
"COLOUR_NAME="+p.ManufacturerColour+"\r\n"+
"COLOUR_HEX="+p.Color+"\r\n"+
"NOZZLE="+p.NozzleMin+"-"+p.NozzleMax+"\r\n"+
"BED="+p.BedMin+"-"+p.BedMax+"\r\n"+
"WEIGHT_G="+p.Weight+"\r\n"+
"LENGTH_M="+p.Length+"\r\n"+
"```\r\n\r\n"+
"Generated by ACE Pro 2 RFID Tag Writer v0.6.\r\n";
                    File.WriteAllText(d.FileName,md,Encoding.UTF8);
                    Log("GitHub contribution file exported to "+d.FileName);
                    MessageBox.Show("GitHub contribution file prepared.\r\n\r\nNothing was uploaded automatically.","Export for GitHub");
                }
            } catch(Exception ex){ MessageBox.Show(ex.Message,"GitHub export failed"); }
        }

        string SafeFileName(string s)
        {
            foreach(char c in Path.GetInvalidFileNameChars()) s=s.Replace(c,'_');
            return s;
        }

        void UpdateWeightCheck()
        {
            int newGross=(int)nNewGross.Value;
            int empty=(int)nEmptySpool.Value;
            int gross=(int)nCurrentGross.Value;
            int nominal=ProtocolFilamentWeight();
            int totalLength=(int)nLength.Value;
            int need=(int)nPrintNeed.Value;
            int margin=(int)nSafetyMargin.Value;

            int actualStart=(newGross>empty)?(newGross-empty):nominal;

            if(gross<=0 || actualStart<=0) {
                lblRemaining.Text="Remaining: enter current gross reel weight";
                lblPrintCheck.Text="Print check: —";
                return;
            }

            int grams=gross-empty;
            if(grams<0) grams=0;
            double pct=100.0*grams/actualStart;
            if(pct>100.0) pct=100.0;
            double metres=totalLength*((double)grams/actualStart);
            if(metres<0) metres=0;

            lblRemaining.Text="Remaining: "+grams+" g  |  "+pct.ToString("0.0")+"%  |  approx "+metres.ToString("0")+" m";

            if(need<=0) lblPrintCheck.Text="Print check: enter slicer-required grams";
            else if(grams>=need+margin) lblPrintCheck.Text="Print check: SAFE — "+(grams-need-margin)+" g above print + safety margin";
            else if(grams>=need) lblPrintCheck.Text="Print check: TIGHT — enough for print, but below safety margin";
            else lblPrintCheck.Text="Print check: NOT ENOUGH — short by "+(need-grams)+" g";
        }

        void UpdateFromGitHub(object sender, EventArgs e)
        {
            const string url="https://raw.githubusercontent.com/DnG-Crafts/ACE-RFID/main/Windows/ACE%20RFID/MatDb.cs";
            try {
                Log("Downloading current ACE-RFID material database from GitHub...");
                // GitHub requires modern TLS. Older .NET Framework defaults may negotiate TLS 1.0.
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
                string src;
                using(var wc=new WebClient()) {
                    wc.Headers.Add("User-Agent","ACE-Pro-2-Tag-Writer");
                    src=wc.DownloadString(url);
                }
                List<Profile> items=ParseAceRfidMatDb(src);
                if(items.Count==0) throw new Exception("No profiles could be parsed from the current MatDb.cs.");

                // Replace the previous GitHub set so the count reflects the current upstream file.
                var oldGitHub=new List<string>();
                foreach(string k in profiles.Keys) if(k.StartsWith("GitHub ACE-RFID - ")) oldGitHub.Add(k);
                foreach(string k in oldGitHub) profiles.Remove(k);

                for(int i=0;i<items.Count;i++) {
                    Profile p=items[i];
                    string source=string.IsNullOrWhiteSpace(p.SourceName) ? p.Material : p.SourceName;
                    string vendor=string.IsNullOrWhiteSpace(p.Manufacturer) ? "Generic" : p.Manufacturer;
                    string baseName="GitHub ACE-RFID - "+vendor+" "+source;
                    string name=baseName;
                    int suffix=2;
                    while(profiles.ContainsKey(name)) name=baseName+" ("+(suffix++)+")";
                    profiles[name]=p;
                }
                SaveGlobalProfiles(items);
                RefreshProfileList();
                Log("GitHub update complete: "+items.Count+" profiles imported.");
                MessageBox.Show(items.Count+" upstream material profiles imported.\r\n\r\n"+
                    "The current ACE-RFID database mainly supplies material/vendor/SKU/temperature defaults. "+
                    "Check colour, weight, length and empty spool weight before writing a tag.",
                    "GitHub update complete");
            } catch(Exception ex) {
                Log("GITHUB UPDATE FAILED: "+ex.Message);
                MessageBox.Show(ex.Message,"GitHub update failed");
            }
        }

        List<Profile> ParseAceRfidMatDb(string src)
        {
            var list=new List<Profile>();

            // Current ACE-RFID MatDb.cs populates its defaults with:
            // doc.Root.Add(new XElement("Filament",
            //   new XElement("Position", ...),
            //   new XElement("FilamentId", "..."),
            //   new XElement("FilamentName", "..."),
            //   new XElement("FilamentVendor", "..."),
            //   new XElement("FilamentParam", "min|max|bedmin|bedmax")));
            //
            // Match complete entries directly instead of trying to parse nested
            // XElement parentheses generically.
            string pattern =
                @"new\s+XElement\(""Filament"",\s*" +
                @"new\s+XElement\(""Position"",\s*[^)]*\)\s*,\s*" +
                @"new\s+XElement\(""FilamentId"",\s*""(?<id>[^""]*)""\)\s*,\s*" +
                @"new\s+XElement\(""FilamentName"",\s*""(?<name>[^""]*)""\)\s*,\s*" +
                @"new\s+XElement\(""FilamentVendor"",\s*""(?<vendor>[^""]*)""\)\s*,\s*" +
                @"new\s+XElement\(""FilamentParam"",\s*""(?<param>[^""]*)""\)\s*\)";

            MatchCollection matches=Regex.Matches(src,pattern,RegexOptions.Singleline);
            foreach(Match m in matches) {
                string id=m.Groups["id"].Value;
                string name=m.Groups["name"].Value;
                string vendor=m.Groups["vendor"].Value;
                string param=m.Groups["param"].Value;

                string[] nums=param.Split('|');
                int nmin,nmax,bmin,bmax;
                if(nums.Length<4 ||
                   !int.TryParse(nums[0],out nmin) ||
                   !int.TryParse(nums[1],out nmax) ||
                   !int.TryParse(nums[2],out bmin) ||
                   !int.TryParse(nums[3],out bmax)) continue;

                // Tag material field is only four bytes. Keep the upstream full name
                // in Manufacturer/profile naming, but use a safe material token.
                string tagMaterial=name;
                if(tagMaterial.Length>4) {
                    if(tagMaterial.StartsWith("PLA",StringComparison.OrdinalIgnoreCase)) tagMaterial="PLA";
                    else if(tagMaterial.StartsWith("PETG",StringComparison.OrdinalIgnoreCase)) tagMaterial="PETG";
                    else if(tagMaterial.StartsWith("TPU",StringComparison.OrdinalIgnoreCase)) tagMaterial="TPU";
                    else if(tagMaterial.StartsWith("ABS",StringComparison.OrdinalIgnoreCase)) tagMaterial="ABS";
                    else if(tagMaterial.StartsWith("ASA",StringComparison.OrdinalIgnoreCase)) tagMaterial="ASA";
                    else tagMaterial=tagMaterial.Substring(0,4);
                }

                list.Add(new Profile{
                    Manufacturer=vendor,
                    Code=vendor,
                    Material=tagMaterial,
                    SourceName=name,
                    Sku=id,
                    Color="#000000",
                    NozzleMin=nmin,NozzleMax=nmax,
                    BedMin=bmin,BedMax=bmax,
                    Weight=1000,Length=330,EmptySpool=0,NewGross=0,Verified=false
                });
            }
            return list;
        }

        string ExtractXElement(string body,string field)
        {
            Match m=Regex.Match(body, @"new\s+XElement\("""+Regex.Escape(field)+@""",\s*""(?<v>[^""]*)""\)");
            return m.Success ? m.Groups["v"].Value : "";
        }

        void SaveGlobalProfiles(List<Profile> items)
        {
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ACEPro2TagWriter");
            Directory.CreateDirectory(dir);
            string path=Path.Combine(dir,"github_profiles.tsv");
            var rows=new List<string>();

            foreach(Profile p in items) {
                string source=string.IsNullOrWhiteSpace(p.SourceName) ? p.Material : p.SourceName;
                string vendor=string.IsNullOrWhiteSpace(p.Manufacturer) ? "Generic" : p.Manufacturer;
                string name="GitHub ACE-RFID - "+vendor+" "+source;

                rows.Add(string.Join("\t",new string[]{name,p.Manufacturer,p.Code,p.Material,p.Sku,p.Color,
                    p.NozzleMin.ToString(),p.NozzleMax.ToString(),p.BedMin.ToString(),p.BedMax.ToString(),
                    p.Weight.ToString(),p.Length.ToString(),p.EmptySpool.ToString(),"0",source}));
            }
            File.WriteAllLines(path,rows.ToArray());
        }

        void LoadGlobalProfiles()
        {
            try {
                string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"ACEPro2TagWriter");
                string path=Path.Combine(dir,"github_profiles.tsv");
                if(!File.Exists(path)) return;

                var oldGitHub=new List<string>();
                foreach(string k in profiles.Keys) if(k.StartsWith("GitHub ACE-RFID - ")) oldGitHub.Add(k);
                foreach(string k in oldGitHub) profiles.Remove(k);

                foreach(string row in File.ReadAllLines(path)) {
                    string[] x=row.Split('\t');
                    if(x.Length<12) continue;
                    int a,b,c,d,w,l,empty=0;
                    if(!int.TryParse(x[6],out a) || !int.TryParse(x[7],out b) || !int.TryParse(x[8],out c) ||
                       !int.TryParse(x[9],out d) || !int.TryParse(x[10],out w) || !int.TryParse(x[11],out l)) continue;
                    if(x.Length>=13) int.TryParse(x[12],out empty);
                    string source=x.Length>=15 ? x[14] : "";

                    string key=x[0];
                    string unique=key; int suffix=2;
                    while(profiles.ContainsKey(unique)) unique=key+" ("+(suffix++)+")";

                    profiles[unique]=new Profile{
                        Manufacturer=x[1],Code=x[2],Material=x[3],Sku=x[4],Color=x[5],SourceName=source,
                        NozzleMin=a,NozzleMax=b,BedMin=c,BedMax=d,Weight=w,Length=l,
                        EmptySpool=empty,NewGross=0,Verified=false
                    };
                }
                RefreshChoiceListsFromProfiles();
            } catch(Exception ex){ Log("GLOBAL PROFILE LOAD FAILED: "+ex.Message); }
        }


        void RefreshProfileList()
        {
            loadingProfile=true;
            string old=SelectedProfileKey();

            lbProfiles.Items.Clear();
            var names=new List<string>(profiles.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);

            foreach(string key in names) {
                Profile p=profiles[key];
                lbProfiles.Items.Add(key);
            }

            if(old!="") {
                for(int i=0;i<lbProfiles.Items.Count;i++) {
                    string shown=lbProfiles.Items[i].ToString();
                    string key=shown;
                    if(string.Equals(key,old,StringComparison.OrdinalIgnoreCase)) {
                        lbProfiles.SelectedIndex=i;
                        break;
                    }
                }
            }

            RefreshChoiceListsFromProfiles();
            loadingProfile=false;
        }

        void MakeSkuFromUid(object sender, EventArgs e)
        {
            Pcsc.Connection c=null;
            try {
                c=Open();
                string uid=Pcsc.GetUid(c).Replace(":","");
                string code=(txtCode.Text.Trim()+txtMaterial.Text.Trim()).ToUpperInvariant();
                code=new string(Array.FindAll(code.ToCharArray(), ch => char.IsLetterOrDigit(ch)));
                if(code.Length>4) code=code.Substring(0,4);
                string tail=uid.Length>7 ? uid.Substring(uid.Length-7) : uid;
                string sku=(code+tail).ToUpperInvariant();
                if(sku.Length>12) sku=sku.Substring(0,12);
                txtSku.Text=sku;
                Log("Generated SKU from tag UID: "+sku);
            } catch(Exception ex){ Log("SKU GENERATION FAILED: "+ex.Message); MessageBox.Show(ex.Message,"SKU generation failed"); }
            finally { if(c!=null) c.Dispose(); }
        }
    }

    public class Profile
    {
        public string Manufacturer="",Code="",Material="",Sku="",Color="",ManufacturerColour="",SourceName="";
        public int NozzleMin,NozzleMax,BedMin,BedMax,Weight,Length,EmptySpool,NewGross;
        public bool Verified;
    }

    public static class Ace
    {
        public static SortedDictionary<int,byte[]> BuildMap(Profile p)
        {
            var m=new SortedDictionary<int,byte[]>();
            for(int pg=0x04;pg<=0x27;pg++) m[pg]=new byte[]{0,0,0,0};
            m[0x04]=new byte[]{0x7B,0x00,0x65,0x00};
            PutAscii(m,0x05,3,p.Sku);
            PutAscii(m,0x0A,2,p.Code);
            PutAscii(m,0x0F,1,p.Material);

            string h=p.Color.Replace("#",""); byte r=Convert.ToByte(h.Substring(0,2),16), g=Convert.ToByte(h.Substring(2,2),16), b=Convert.ToByte(h.Substring(4,2),16);
            m[0x14]=new byte[]{0xFF,b,g,r}; // ACE NFC page 14 is ABGR; UI remains #RRGGBB
            m[0x17]=new byte[]{0x32,0x00,0xC8,0x00};
            m[0x18]=U16Pair(p.NozzleMin,p.NozzleMax);
            m[0x1D]=U16Pair(p.BedMin,p.BedMax);
            byte[] len=U16(p.Length); m[0x1E]=new byte[]{0xAF,0x00,len[0],len[1]};
            m[0x1F]=U32(p.Weight);
            return m;
        }
        static void PutAscii(SortedDictionary<int,byte[]> m,int start,int pages,string s)
        {
            byte[] buf=new byte[pages*4], raw=Encoding.ASCII.GetBytes(s);
            Array.Copy(raw,buf,Math.Min(buf.Length,raw.Length));
            for(int i=0;i<pages;i++){ byte[] p=new byte[4]; Array.Copy(buf,i*4,p,0,4); m[start+i]=p; }
        }
        static byte[] U16(int v){ return new byte[]{(byte)(v&255),(byte)((v>>8)&255)}; }
        static byte[] U16Pair(int a,int b){ var x=U16(a);var y=U16(b);return new byte[]{x[0],x[1],y[0],y[1]};}
        static byte[] U32(int v){ return new byte[]{(byte)(v&255),(byte)((v>>8)&255),(byte)((v>>16)&255),(byte)((v>>24)&255)}; }
        public static byte[] MapToBytes(SortedDictionary<int,byte[]> m){ var a=new byte[144];int o=0;for(int p=0x04;p<=0x27;p++){Array.Copy(m[p],0,a,o,4);o+=4;}return a; }
        public static byte[] ReadArea(Pcsc.Connection c){ var a=new byte[144];int o=0;int[] starts={0x04,0x08,0x0C,0x10,0x14,0x18,0x1C,0x20,0x24};foreach(int s in starts){var x=Pcsc.Read4(c,s);Array.Copy(x,0,a,o,16);o+=16;}return a;}
        public static bool IsBlank(byte[] a)
        {
            // Blank NTAGs can contain an NDEF marker in page 04. Treat a tag as blank
            // when the ACE identity fields are empty and weight/length are zero.
            var p=Decode(a);
            return string.IsNullOrWhiteSpace(p.Sku) &&
                   string.IsNullOrWhiteSpace(p.Code) &&
                   string.IsNullOrWhiteSpace(p.Material) &&
                   p.Weight==0 && p.Length==0;
        }
        public static bool BytesEqual(byte[] a,byte[] b){ if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        public static string FirstMismatch(byte[] got,byte[] expected)
        {
            int n=Math.Min(got.Length,expected.Length);
            for(int i=0;i<n;i++) if(got[i]!=expected[i]) {
                int page=0x04+(i/4), off=i%4;
                return "First mismatch at page "+page.ToString("X2")+" byte "+off+
                       ": tag "+got[i].ToString("X2")+" expected "+expected[i].ToString("X2")+".";
            }
            if(got.Length!=expected.Length) return "Length differs.";
            return "";
        }

        public static Profile Decode(byte[] a)
        {
            Func<int,int,byte[]> sl=(pg,n)=>{var x=new byte[n];Array.Copy(a,(pg-0x04)*4,x,0,n);return x;};
            Func<byte[],string> asc=x=>Encoding.ASCII.GetString(x).TrimEnd('\0');
            var c=sl(0x14,4);var noz=sl(0x18,4);var b=sl(0x1D,4);var l=sl(0x1E,4);var w=sl(0x1F,4);
            return new Profile{
                Sku=asc(sl(0x05,12)), Code=asc(sl(0x0A,8)), Material=asc(sl(0x0F,4)),
                Color="#"+c[3].ToString("X2")+c[2].ToString("X2")+c[1].ToString("X2"), // ABGR -> #RRGGBB
                NozzleMin=noz[0]+256*noz[1],NozzleMax=noz[2]+256*noz[3],BedMin=b[0]+256*b[1],BedMax=b[2]+256*b[3],
                Length=l[2]+256*l[3],Weight=w[0]+256*w[1]+65536*w[2]+16777216*w[3]
            };
        }
    }

    public static class Pcsc
    {
        const uint SCOPE_USER=0, SHARE_SHARED=2, PROTO_T0=1, PROTO_T1=2, LEAVE=0;
        [StructLayout(LayoutKind.Sequential)] struct IOREQ { public uint proto; public uint len; }
        [DllImport("winscard.dll")] static extern int SCardEstablishContext(uint s,IntPtr a,IntPtr b,out IntPtr c);
        [DllImport("winscard.dll",CharSet=CharSet.Unicode)] static extern int SCardListReaders(IntPtr c,string g,IntPtr r,ref uint n);
        [DllImport("winscard.dll",CharSet=CharSet.Unicode)] static extern int SCardConnect(IntPtr c,string r,uint sh,uint p,out IntPtr h,out uint ap);
        [DllImport("winscard.dll")] static extern int SCardDisconnect(IntPtr h,uint d);
        [DllImport("winscard.dll")] static extern int SCardReleaseContext(IntPtr c);
        [DllImport("winscard.dll")] static extern int SCardTransmit(IntPtr h,ref IOREQ io,byte[] s,uint sl,IntPtr rio,byte[] r,ref uint rl);

        public sealed class Connection:IDisposable
        {
            internal IntPtr C,H; internal uint P;
            public void Dispose(){ if(H!=IntPtr.Zero)SCardDisconnect(H,LEAVE);if(C!=IntPtr.Zero)SCardReleaseContext(C);H=C=IntPtr.Zero;}
        }

        public static string[] ListReaders()
        {
            IntPtr c;Check(SCardEstablishContext(SCOPE_USER,IntPtr.Zero,IntPtr.Zero,out c),"Establish context");
            try{
                uint n=0;int rc=SCardListReaders(c,null,IntPtr.Zero,ref n);if(rc!=0||n==0)return new string[0];
                IntPtr p=Marshal.AllocHGlobal((int)n*2);
                try{Check(SCardListReaders(c,null,p,ref n),"List readers");string m=Marshal.PtrToStringUni(p,(int)n);return m.Split(new char[]{'\0'},StringSplitOptions.RemoveEmptyEntries);}
                finally{Marshal.FreeHGlobal(p);}
            }finally{SCardReleaseContext(c);}
        }

        public static Connection Open(string reader)
        {
            IntPtr c,h;uint p;
            Check(SCardEstablishContext(SCOPE_USER,IntPtr.Zero,IntPtr.Zero,out c),"Establish context");
            int rc=SCardConnect(c,reader,SHARE_SHARED,PROTO_T0|PROTO_T1,out h,out p);
            if(rc!=0){SCardReleaseContext(c);throw new Exception("Could not connect to tag/reader. PC/SC error 0x"+rc.ToString("X8"));}
            return new Connection{C=c,H=h,P=p};
        }

        static byte[] Tx(Connection c,byte[] cmd)
        {
            var io=new IOREQ{proto=c.P,len=(uint)Marshal.SizeOf(typeof(IOREQ))};
            byte[] r=new byte[300];uint rl=(uint)r.Length;
            Check(SCardTransmit(c.H,ref io,cmd,(uint)cmd.Length,IntPtr.Zero,r,ref rl),"Transmit");
            byte[] o=new byte[rl];Array.Copy(r,o,rl);
            if(o.Length<2||o[o.Length-2]!=0x90||o[o.Length-1]!=0x00)throw new Exception("APDU failed: "+Hex(o));
            if(o.Length>=3&&o[0]==0xD5&&o[1]==0x41&&o[2]!=0)throw new Exception("PN532 returned error: "+Hex(o));
            return o;
        }

        public static string GetUid(Connection c)
        {
            byte[] o=Tx(c,HexBytes("FFCA000000"));byte[] u=new byte[o.Length-2];Array.Copy(o,u,u.Length);return Hex(u,":");
        }
        public static byte[] Read4(Connection c,int page)
        {
            byte[] o=Tx(c,HexBytes("FF00000005D4400130"+page.ToString("X2")));
            if(o.Length<21||o[0]!=0xD5||o[1]!=0x41)throw new Exception("Unexpected read response.");
            byte[] d=new byte[16];Array.Copy(o,3,d,0,16);return d;
        }
        public static void WritePage(Connection c,int page,byte[] data)
        {
            string cmd="FF00000009D44001A2"+page.ToString("X2")+Hex(data);Tx(c,HexBytes(cmd));
        }
        static byte[] HexBytes(string h){h=h.Replace(" ","");byte[] b=new byte[h.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(h.Substring(i*2,2),16);return b;}
        static string Hex(byte[] b,string sep=""){var s=new StringBuilder();for(int i=0;i<b.Length;i++){if(i>0)s.Append(sep);s.Append(b[i].ToString("X2"));}return s.ToString();}
        static void Check(int rc,string what){if(rc!=0)throw new Exception(what+" failed. PC/SC error 0x"+rc.ToString("X8"));}
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
