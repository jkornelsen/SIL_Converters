﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace SilEncConverters40
{
    public partial class ViewSourceFormsForm : Form
    {
        public new AdaptItKBReader Parent { get; set; }
        private MapOfMaps _mapOfMaps;
        private char[] _achTrimSource, _achTrimTarget;

        internal ViewSourceFormsForm(MapOfMaps mapOfMaps, 
            string strSourceWordFont, string strTargetWordFont, 
            char[] achTrimSource, char[] achTrimTarget)
        {
            InitializeComponent();
            _mapOfMaps = mapOfMaps;
            _achTrimSource = achTrimSource;
            _achTrimTarget = achTrimTarget;

            foreach (var strSourceWord in mapOfMaps.ListOfAllSourceWordForms)
                listBoxSourceWordForms.Items.Add(strSourceWord);
            
            targetFormDisplayControl.TargetWordFont = new Font(strTargetWordFont, 12);
            targetFormDisplayControl.CallToSetModified = SetModified;
            textBoxFilter.Font = listBoxSourceWordForms.Font 
                = new Font(strSourceWordFont, 12);
        }

        private const string CstrButtonLabelSave = "&Save";
        private const string CstrButtonLabelReturn = "&Return";

        private void buttonOK_Click(object sender, EventArgs e)
        {
            if (buttonOK.Text == CstrButtonLabelSave)
            {
                System.Diagnostics.Debug.Assert(targetFormDisplayControl.AreAllTargetFormsNonEmpty(_achTrimTarget));
                string strSelectedWord = SelectedWord;
                Parent.SaveMapOfMaps(_mapOfMaps);
                buttonOK.Text = CstrButtonLabelReturn;
                listBoxSourceWordForms.Enabled = true;
                listBoxSourceWordForms.SelectedIndex = -1;
                listBoxSourceWordForms.SelectedItem = strSelectedWord;
                return;
            }

            targetFormDisplayControl.TrimTargetWordForms(_achTrimTarget);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void ViewSourceFormsForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (buttonOK.Text != CstrButtonLabelSave)
                return;

            string strSelectedWord = SelectedWord;
            if ((_mapSourceWordElements != null) &&
                (_copyOfSelectedSourceWord != null))
            {
                _mapSourceWordElements.ReplaceSourceWordElement(strSelectedWord,
                                                                _copyOfSelectedSourceWord);
            }
            buttonOK.Text = CstrButtonLabelReturn;
            listBoxSourceWordForms.Enabled = true;
            listBoxSourceWordForms.SelectedIndex = -1;
            listBoxSourceWordForms.SelectedItem = strSelectedWord;
            e.Cancel = true;
        }

        public string SelectedWord 
        { 
            get
            {
                if (listBoxSourceWordForms.SelectedItem != null)
                    return listBoxSourceWordForms.SelectedItem.ToString();
                return null;
            }
            set
            {
                textBoxFilter.Text = value;
            }
        }

        private void SetModified()
        {
            buttonOK.Text = CstrButtonLabelSave;
            buttonOK.Enabled = targetFormDisplayControl.AreAllTargetFormsNonEmpty(_achTrimTarget);
            listBoxSourceWordForms.Enabled = false;
        }

        private MapOfSourceWordElements _mapSourceWordElements;
        private XElement _copyOfSelectedSourceWord;
        private void listBoxSourceWordForms_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSourceWordForms.SelectedIndex != -1)
            {
                buttonOK.Enabled = true;
                targetFormDisplayControl.Reset();
                string strSourceWord = listBoxSourceWordForms.SelectedItem.ToString();
                if (_mapOfMaps.TryGetValue(strSourceWord, out _mapSourceWordElements))
                {
                    SourceWordElement sourceWordElement;
                    if (_mapSourceWordElements.TryGetValue(strSourceWord, out sourceWordElement))
                    {
                        _copyOfSelectedSourceWord = new XElement(sourceWordElement.Xml);
                        targetFormDisplayControl.Initialize(sourceWordElement, DeleteSourceWord);
                    }
                }
            }
            else
            {
                buttonOK.Enabled = false;
                targetFormDisplayControl.Reset();
            }
        }

        private void DeleteSourceWord(string strSourceWord)
        {
            DialogResult res = MessageBox.Show(String.Format(Properties.Resources.IDS_ConfirmDeleteSourceWord,
                                                             strSourceWord), EncConverters.cstrCaption,
                                               MessageBoxButtons.YesNoCancel);
            if (res != DialogResult.Yes) 
                return;

            _mapSourceWordElements.Remove(strSourceWord);
            RemoveFromForm(strSourceWord);
        }

        private void RemoveFromForm(string strSourceWord)
        {
            listBoxSourceWordForms.Items.Remove(strSourceWord);
            targetFormDisplayControl.Reset();
            SetModified();
        }

        private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBoxSourceWordForms.SelectedIndex != -1)
                DeleteSourceWord(listBoxSourceWordForms.SelectedItem.ToString());
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.Assert(listBoxSourceWordForms.SelectedIndex != -1);
            string strSourceWordToEdit = listBoxSourceWordForms.SelectedItem.ToString();
            var dlg = new AddNewSourceWordForm
                          {
                              Font = textBoxFilter.Font,
                              WordAdded = strSourceWordToEdit
                          };
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            string strNewWord = dlg.WordAdded.Trim(_achTrimSource);
            SourceWordElement sourceWordElement = _mapOfMaps.ChangeSourceWord(strSourceWordToEdit, strNewWord);

            // remove it from the lst box and then add it back (since it might now go in a different map)
            RemoveFromForm(strSourceWordToEdit);

            if (sourceWordElement != null)
            {
                if (!listBoxSourceWordForms.Items.Contains(sourceWordElement.SourceWord))
                    listBoxSourceWordForms.Items.Add(sourceWordElement.SourceWord);
                listBoxSourceWordForms.SelectedItem = sourceWordElement.SourceWord;
            }
        }

        private void textBoxFilter_TextChanged(object sender, EventArgs e)
        {
            int nIndex = listBoxSourceWordForms.FindString(textBoxFilter.Text);
            listBoxSourceWordForms.TopIndex = nIndex;
            listBoxSourceWordForms.SelectedIndex = nIndex;
        }

        private void buttonAddNewSourceWord_Click(object sender, EventArgs e)
        {
            var dlg = new AddNewSourceWordForm 
                            {
                                Font = textBoxFilter.Font, 
                                WordAdded = textBoxFilter.Text
                            };
            if (dlg.ShowDialog() != DialogResult.OK) 
                return;

            string strNewSource = dlg.WordAdded.Trim(_achTrimSource);
            string strNewTarget = dlg.WordAdded.Trim(_achTrimTarget);
            _mapOfMaps.AddCouplet(strNewSource, strNewTarget);

            if (!listBoxSourceWordForms.Items.Contains(strNewSource))
                listBoxSourceWordForms.Items.Add(strNewSource);
            listBoxSourceWordForms.SelectedItem = strNewSource;

            SetModified();
        }

        private void contextMenuStrip_Opening(object sender, CancelEventArgs e)
        {
            editToolStripMenuItem.Enabled 
                = deleteToolStripMenuItem.Enabled 
                = (listBoxSourceWordForms.SelectedIndex != -1);
        }
    }
}
