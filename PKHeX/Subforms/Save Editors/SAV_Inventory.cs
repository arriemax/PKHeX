﻿using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PKHeX
{
    public partial class SAV_Inventory : Form
    {
        public SAV_Inventory()
        {
            InitializeComponent();
            Util.TranslateInterface(this, Main.curlanguage);
            if (SAV.Generation <= 3)
                B_GiveAll.Visible = false;
            itemlist = Main.GameStrings.getItemStrings(SAV.Generation, SAV.Version);

            for (int i = 0; i < itemlist.Length; i++)
                if (itemlist[i] == "")
                    itemlist[i] = $"(Item #{i.ToString("000")})";
            Pouches = SAV.Inventory;
            initBags();
            getBags();
        }

        private readonly SaveFile SAV = Main.SAV.Clone();
        private readonly InventoryPouch[] Pouches;
        private const string TabPrefix = "TAB_";
        private const string DGVPrefix = "DGV_";

        private void B_Cancel_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void B_Save_Click(object sender, EventArgs e)
        {
            setBags();
            SAV.Inventory = Pouches;
            Array.Copy(SAV.Data, Main.SAV.Data, SAV.Data.Length);
            Main.SAV.Edited = true;
            Close();
        }

        private void initBags()
        {
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.ItemSize = new Size(IL_Pouch.Images[0].Width + 4, 0);
            for (int i = 0; i < Pouches.Length; i++)
            {
                // Add Tab
                tabControl1.TabPages.Add(new TabPage
                {
                    // Text = Pouches[i].Type.ToString(),
                    ImageIndex = (int)Pouches[i].Type
                });

                tabControl1.TabPages[i].Controls.Add(getDGV(Pouches[i]));
            }
        }
        private DataGridView getDGV(InventoryPouch pouch)
        {
            // Add DataGrid
            DataGridView dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                Text = pouch.Type.ToString(),
                Name = DGVPrefix + pouch.Type,

                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToResizeColumns = false,
                RowHeadersVisible = false,
                ColumnHeadersVisible = false,
                MultiSelect = false,
                ShowEditingIcon = false,

                EditMode = DataGridViewEditMode.EditOnEnter,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                CellBorderStyle = DataGridViewCellBorderStyle.None,
            };

            DataGridViewComboBoxColumn dgvItemVal = new DataGridViewComboBoxColumn
            {
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                DisplayIndex = 0,
                Width = 135,
                FlatStyle = FlatStyle.Flat
            };
            DataGridViewColumn dgvIndex = new DataGridViewTextBoxColumn();
            {
                dgvIndex.HeaderText = "CNT";
                dgvIndex.DisplayIndex = 1;
                dgvIndex.Width = 45;
                dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            dgv.Columns.Add(dgvItemVal);
            dgv.Columns.Add(dgvIndex);

            var itemcount = pouch.Items.Length;
            string[] itemarr = Main.HaX ? (string[])itemlist.Clone() : getItems(pouch.LegalItems);

            var combo = dgv.Columns[0] as DataGridViewComboBoxColumn;
            foreach (string t in itemarr)
                combo.Items.Add(t); // add only the Item Names

            dgv.Rows.Add(itemcount > 0 ? itemcount : itemarr.Length);
            dgv.CancelEdit();

            return dgv;
        }
        private void getBags()
        {
            foreach (InventoryPouch pouch in Pouches)
            {
                DataGridView dgv = Controls.Find(DGVPrefix + pouch.Type, true).FirstOrDefault() as DataGridView;

                // Sanity Screen
                var invalid = pouch.Items.Where(item => item.Index != 0 && !pouch.LegalItems.Contains((ushort)item.Index)).ToArray();
                var outOfBounds = invalid.Where(item => item.Index >= itemlist.Length).ToArray();
                var incorrectPouch = invalid.Where(item => item.Index < itemlist.Length).ToArray();
                pouch.Items = pouch.Items.Where(item => item.Index == 0 || pouch.LegalItems.Contains((ushort)item.Index)).ToArray();

                if (outOfBounds.Any())
                    Util.Error("Unknown item detected.", "Item ID(s): " + string.Join(", ", outOfBounds.Select(item => item.Index)));
                if (incorrectPouch.Any())
                    Util.Alert($"The following item(s) have been removed from {pouch.Type} pouch.",
                        string.Join(", ", incorrectPouch.Select(item => itemlist[item.Index])), 
                        "If you save changes, the item(s) will no longer be in the pouch.");

                int purgedItems = outOfBounds.Length + incorrectPouch.Length;
                pouch.Items = pouch.Items.Concat(new byte[purgedItems].Select(i => new InventoryItem())).ToArray();

                getBag(dgv, pouch);
            }
        }
        private void setBags()
        {
            foreach (InventoryPouch t in Pouches)
            {
                DataGridView dgv = Controls.Find(DGVPrefix + t.Type, true).FirstOrDefault() as DataGridView;
                setBag(dgv, t);
            }
        }
        private void getBag(DataGridView dgv, InventoryPouch pouch)
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                dgv.Rows[i].Cells[0].Value = itemlist[pouch.Items[i].Index];
                dgv.Rows[i].Cells[1].Value = pouch.Items[i].Count;
            }
        }
        private void setBag(DataGridView dgv, InventoryPouch pouch)
        {
            int ctr = 0;
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                string item = dgv.Rows[i].Cells[0].Value.ToString();
                int itemindex = Array.IndexOf(itemlist, item);
                if (itemindex <= 0) // Compression of Empty Slots
                    continue;

                int itemcnt;
                int.TryParse(dgv.Rows[i].Cells[1].Value.ToString(), out itemcnt);

                if (Main.HaX && SAV.Generation != 7) // Gen7 has true cap at 1023, keep 999 cap.
                {
                    // Cap at absolute maximum
                    if (SAV.Generation <= 2 && itemcnt > byte.MaxValue)
                        itemcnt = byte.MaxValue;
                    else if (SAV.Generation >= 3 && itemcnt > ushort.MaxValue)
                        itemcnt = ushort.MaxValue;
                }
                else if (itemcnt > pouch.MaxCount)
                    itemcnt = pouch.MaxCount; // Cap at pouch maximum
                else if (itemcnt <= 0)
                    continue; // ignore item

                pouch.Items[ctr++] = new InventoryItem { Index = itemindex, Count = itemcnt };
            }
            for (int i = ctr; i < pouch.Items.Length; i++)
                pouch.Items[i] = new InventoryItem(); // Empty Slots at the end
        }

        // Initialize String Tables
        private readonly string[] itemlist;
        private string[] getItems(ushort[] items, bool sort = true)
        {
            string[] res = new string[items.Length + 1];
            for (int i = 0; i < res.Length - 1; i++)
                res[i] = itemlist[items[i]];
            res[items.Length] = itemlist[0];
            if (sort)
                Array.Sort(res);
            return res;
        }

        // User Cheats
        private int CurrentPouch => tabControl1.SelectedIndex;
        private void B_GiveAll_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            giveMenu.Show(btn.PointToScreen(new Point(0, btn.Height)));
        }
        private void giveAllItems(object sender, EventArgs e)
        {
            // Get Current Pouch
            int pouch = CurrentPouch;
            if (pouch < 0)
                return;
            ushort[] legalitems = Pouches[pouch].LegalItems;

            DataGridView dgv = Controls.Find(DGVPrefix + Pouches[pouch].Type, true).FirstOrDefault() as DataGridView;

            int Count = Pouches[pouch].MaxCount;
            for (int i = 0; i < legalitems.Length; i++)
            {
                int item = legalitems[i];
                string itemname;
                int c = Count;

                // Override for HMs
                switch (SAV.Generation)
                {
                    case 3: {
                        itemname = itemlist[item];
                        if (Legal.Pouch_HM_RS.Contains(legalitems[i])) c = 1;
                        break;
                    }
                    default: {
                        itemname = itemlist[item];
                        if (new[] { 420, 421, 422, 423, 423, 424, 425, 426, 427, 737 }.Contains(legalitems[i])) c = 1;
                        break;
                    }
                }

                dgv.Rows[i].Cells[0].Value = itemname;
                dgv.Rows[i].Cells[1].Value = c;
            }
            System.Media.SystemSounds.Asterisk.Play();
        }
        private void removeAllItems(object sender, EventArgs e)
        {
            // Get Current Pouch
            int pouch = CurrentPouch;
            if (pouch < 0)
                return;

            DataGridView dgv = Controls.Find(DGVPrefix + Pouches[pouch].Type, true).FirstOrDefault() as DataGridView;
            
            for (int i = 0; i < dgv.RowCount; i++)
            {
                dgv.Rows[i].Cells[0].Value = itemlist[0];
                dgv.Rows[i].Cells[1].Value = 0;
            }
            Util.Alert("Items cleared.");
        }
        private void B_Sort_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;
            sortMenu.Show(btn.PointToScreen(new Point(0, btn.Height)));
        }
        private void sortByName(object sender, EventArgs e)
        {
            int pouch = CurrentPouch;
            var dgv = Controls.Find(DGVPrefix + Pouches[pouch].Type, true).FirstOrDefault() as DataGridView;
            var p = Pouches[pouch];
            setBag(dgv, p);
            if (sender == mnuSortName)
                p.sortName(itemlist, reverse:false);
            if (sender == mnuSortNameReverse)
                p.sortName(itemlist, reverse:true);
            getBag(dgv, p);
        }
        private void sortByCount(object sender, EventArgs e)
        {
            int pouch = CurrentPouch;
            var dgv = Controls.Find(DGVPrefix + Pouches[pouch].Type, true).FirstOrDefault() as DataGridView;
            var p = Pouches[pouch];
            setBag(dgv, p);
            if (sender == mnuSortCount)
                p.sortCount(reverse:false);
            if (sender == mnuSortCountReverse)
                p.sortCount(reverse:true);
            getBag(dgv, p);
        }
    }
}
