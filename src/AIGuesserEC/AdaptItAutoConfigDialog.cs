using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ECInterfaces;                     // for IEncConverter
using System.IO;                        // for Directory

namespace SilEncConverters40
{
    public partial class AdaptItAutoConfigDialog : SilEncConverters40.AutoConfigDialog
    {
        protected const string cstrAdaptItWorkingDirLegacy = "Adapt It Work";
        public const string CstrAdaptItWorkingDirUnicode = "Adapt It Unicode Work";
        protected const string cstrAdaptItGlossingKB = "Glossing.xml";
        protected const string cstrAdaptItGlossingKBLabel = " (Glossing Knowledge Base)";

        protected bool m_bLegacy = false;
        protected string m_strXmlTitle = null;
        
        /// <summary>
        /// This is the base class for the two different AdaptIt EncConverters: the Lookup transducer and the 
        /// Guesser API transducer. Since both of these types have the same configuration dialog, most of the
        /// implementation can be put into this class, while the subclasses are used for the specifics to
        /// each (if there is none, then you can just get rid of this class)
        /// </summary>
        public override void Initialize
            (
            IEncConverters aECs,
            string strHtmlFileName,
            string strDisplayName,
            string strFriendlyName,
            string strConverterIdentifier,
            ConvType eConversionType,
            string strLhsEncodingId,
            string strRhsEncodingId,
            int lProcessTypeFlags,
            bool bIsInRepository
            )
        {
            InitializeComponent();

            base.Initialize
            (
            aECs,
            strHtmlFileName,
            strDisplayName,
            strFriendlyName,
            strConverterIdentifier,
            eConversionType,
            strLhsEncodingId,
            strRhsEncodingId,
            lProcessTypeFlags,
            bIsInRepository
            ); 
            
            m_bQueryForConvType = false;    // don't need to do this for this converter type (or we do, but differently)

#if !NotUsingNormalizer
            // in the new situation, the converter spec has two pieces of information:
            //  a) the same as the original one -- the path to the KB file
            //  b) the path to a normalizing entity (e.g. cct, tec, map, or a custom
            //      XML file that has the normalizing correspondences (ala. ???)
            // these two are separated by a ';'
            if (m_bEditMode)
            {
                System.Diagnostics.Debug.Assert(!String.IsNullOrEmpty(strConverterIdentifier));
                string[] astrPaths = strConverterIdentifier.Split(new[] {';'});
                string strKnowledgeBaseFileSpec = astrPaths[0];

                if (astrPaths.Length >= 2)
                    textBoxNormalizationPath.Text = astrPaths[1];
                
                m_bLegacy = (strKnowledgeBaseFileSpec.IndexOf(cstrAdaptItWorkingDirLegacy) != -1);
            }
#else
            if (m_bEditMode)
                m_bLegacy = (strConverterIdentifier.IndexOf(cstrAdaptItWorkingDirLegacy) != -1);
#endif

            if (m_bLegacy)
            {
                InitProjectNames(cstrAdaptItWorkingDirLegacy, true);
                radioButtonLegacy.Checked = true;
            }
            else
            {
                InitProjectNames(CstrAdaptItWorkingDirUnicode, false);
                radioButtonUnicode.Checked = true;
            }

            if (m_bEditMode)
            {
                int nIndex = listBoxProjects.Items.IndexOf(ProjectNameFromConverterSpec);
                if (nIndex != -1)
                    listBoxProjects.SelectedIndex = nIndex;

                IsModified = false;
            }
        }

        protected string ProjectNameFromConverterSpec
        {
            get
            {
                // ConverterIdentifier is any of:
                //  C:\Documents and Settings\Bob\My Documents\Adapt It Unicode Work\Kangri to Hindi adaptations\Kangri to Hindi adaptations.xml
                //  C:\Documents and Settings\Bob\My Documents\Adapt It Unicode Work\Kangri to Hindi adaptations\Kangri to Hindi adaptations.xml;<some path to a normalization file>
                //  C:\Documents and Settings\Bob\My Documents\Adapt It Unicode Work\Kangri to Hindi adaptations\Glossing.xml
                string[] astrParts = ConverterIdentifier.Split(new[] {';'});
                if (astrParts.Length == 0)
                    return null;

                string strKbFileSpec = astrParts[0];
                int nIndex = strKbFileSpec.IndexOf(cstrAdaptItGlossingKB);
                string strProjectName;
                if (nIndex != -1)
                {
                    // if glossing.xml, then get the project name from the parent folder name:
                    strProjectName = Path.GetDirectoryName(strKbFileSpec);        // now: C:\Documents and Settings\Bob\My Documents\Adapt It Unicode Work\Kangri to Hindi adaptations
                    strProjectName = Path.GetFileNameWithoutExtension(strProjectName);  // now: Kangri to Hindi adaptations
                    strProjectName += cstrAdaptItGlossingKBLabel;                       // now: Kangri to Hindi adaptations (Glossing Knowledge Base)
                }
                else
                    strProjectName = Path.GetFileNameWithoutExtension(strKbFileSpec);
                
                return strProjectName;
            }
        }

        protected void InitProjectNames(string strWorkingDirectory, bool bLegacy)
        {
            m_bLegacy = bLegacy;
            listBoxProjects.Items.Clear();

            string strPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                strWorkingDirectory);

            if (Directory.Exists(strPath))
            {
                string[] astrFolders = Directory.GetDirectories(strPath);
                foreach (string strFolder in astrFolders)
                {
                    if (IsAdaptitProjectDirectory(strFolder))
                    {
                        string strProjectName = Path.GetFileNameWithoutExtension(strFolder);
                        listBoxProjects.Items.Add(strProjectName);
                    }

                    // also check for a glossing knowledgebase
                    string strGlossingFileSpec = Path.Combine(strFolder,
                        cstrAdaptItGlossingKB);
                    if( File.Exists(strGlossingFileSpec) )
                        listBoxProjects.Items.Add(Path.GetFileNameWithoutExtension(strFolder) + cstrAdaptItGlossingKBLabel);
                }
            }

            IsModified = true;
        }

        protected bool IsAdaptitProjectDirectory(string strFolderPath)
        {
            if (strFolderPath.IndexOf(" adaptations") == -1)
                return false;
            else if (strFolderPath.IndexOf(" to ") == -1)
                return false;
            else if (strFolderPath.Length < 18)
                return false;
            else
                return true;
        }

        // this method is called either when the user clicks the "Apply" or "OK" buttons *OR* if she
        //  tries to switch to the Test or Advanced tab. This is the dialog's one opportunity
        //  to make sure that the user has correctly configured a legitimate converter.
        protected override bool OnApply()
        {
            // for AdaptIt, we only need an item from the list box selected and the project type (Legacy vs. Unicode)
            if (radioButtonLegacy.Checked)
            {
                m_bLegacy = true;
                ConversionType = ConvType.Legacy_to_from_Legacy;
            }
            else
            {
                System.Diagnostics.Debug.Assert(radioButtonUnicode.Checked);
                m_bLegacy = false;
                ConversionType = ConvType.Unicode_to_from_Unicode;
            }

            string strProjectName = (string)listBoxProjects.SelectedItem;
            if (String.IsNullOrEmpty(strProjectName))
                return false;

            m_strXmlTitle = strProjectName + ".xml";
            if (strProjectName.IndexOf(cstrAdaptItGlossingKBLabel) != -1)
            {
                strProjectName = strProjectName.Substring(0, strProjectName.Length - cstrAdaptItGlossingKBLabel.Length);
                m_strXmlTitle = cstrAdaptItGlossingKB;
            }

            // (Prior to .Net 4.0, Path.Combine handled only two arguments.)
            string strKBFileSpec = Path.Combine(Path.Combine(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                (m_bLegacy) ? cstrAdaptItWorkingDirLegacy : CstrAdaptItWorkingDirUnicode),
                strProjectName),
                m_strXmlTitle);

            ConverterIdentifier = strKBFileSpec;
            
            // see if we need to tack on the normalization file path.
            string strNormalizationFilePath = textBoxNormalizationPath.Text;
            if (!String.IsNullOrEmpty(strNormalizationFilePath))
                ConverterIdentifier += String.Format(";{0}", strNormalizationFilePath);

            // if we're actually on the setup tab, then give the exact error.
            if (tabControl.SelectedTab == tabPageSetup)
            {
                // only do these message boxes if we're on the Setup tab itself, because if this OnApply
                //  is being called as a result of the user switching to the Test tab, that code will
                //  already put up an error message and we don't need two error messages.
                if (!File.Exists(strKBFileSpec))
                {
                    MessageBox.Show(this, "Does your AdaptIt Project store the knowledge base as an XML document? (it has to for this to work)", EncConverters.cstrCaption);
                    return false;
                }
            }

            return base.OnApply();
        }

        private void radioButtonUnicode_Click(object sender, EventArgs e)
        {
            InitProjectNames(CstrAdaptItWorkingDirUnicode, false);
        }

        private void radioButtonLegacy_Click(object sender, EventArgs e)
        {
            InitProjectNames(cstrAdaptItWorkingDirLegacy, true);
        }

        private void listBoxProjects_SelectedIndexChanged(object sender, EventArgs e)
        {
            IsModified = true;
        }

        private void buttonBrowseNormalizationMap_Click(object sender, EventArgs e)
        {
            if (openFileDialogBrowse.ShowDialog() == DialogResult.OK)
            {
                textBoxNormalizationPath.Text = openFileDialogBrowse.FileName;
            }
        }
    }
}

