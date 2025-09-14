using Xceed.Words.NET;
using Xceed.Document.NET;
using CvParser.Domain.Entities.CV.V1;
using Paragraph = Xceed.Document.NET.Paragraph;


namespace CvParser.Infrastructure.Services
{
    [Obsolete]
    public class CvServiceXceed
    {       
        public async Task<MemoryStream> CreateXameraCV(Cv cv)
        {
            string folderPath = @"C:\_DEV\CVs";
            string fileTemplate = @$"{folderPath}\CV-FirstPage-Template.docx";

            using var templateFileStream = File.OpenRead(fileTemplate);
            var resultStream = new MemoryStream();
            templateFileStream.CopyTo(resultStream);
            resultStream.Position = 0;

            using var doc = DocX.Load(resultStream);

            CreateFirstPage(doc, cv);

            NewPage(doc);


            // Add new content on page 2+
            doc.InsertParagraph("This is page 2.")
                .FontSize(16)
                .SpacingAfter(20);

            // Save to a new MemoryStream
            var finalStream = new MemoryStream();
            doc.SaveAs(finalStream);
            finalStream.Position = 0;

            return finalStream;
        }

        private void CreateFirstPage(DocX doc, Cv cv)
        {
            try
            {
                foreach (var paragraph in doc.Paragraphs)
                {
                    switch (paragraph.Text)
                    {
                        case "|Name|":
                            ReplaceText("|Name|", cv.PersonalInfo.Name, doc, paragraph);
                            break;
                        case "|Title|":
                            ReplaceText("|Title|", cv.PersonalInfo.Title, doc, paragraph);
                            break;
                        case "|Summary|":
                            ReplaceText("|Name|", cv.Summary, doc, paragraph);
                            break;
                        case "|Skills|":
                            ReplaceSkillsPlaceholder(doc, "|Skills|", cv.Competencies, paragraph);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }




        private void ReplaceSkillsPlaceholder(DocX doc, string placeholder, List<Competency> competencies)
        {
            // Find all paragraphs containing the placeholder
            var paragraphs = doc.Paragraphs.Where(p => p.Text.Contains(placeholder)).ToList();

            foreach (var paragraph in paragraphs)
            {
                // Remove the placeholder paragraph
                paragraph.Remove(false);

                bool isFirstSkill = true;

                foreach (var competency in competencies)
                {
                    // Add a blank line before each skill except the first one
                    if (!isFirstSkill)
                    {
                        doc.InsertParagraph(); // This will create an empty line
                    }

                    // Skill name paragraph
                    var pSkill = doc.InsertParagraph()
                                    .Font("Arial")
                                    .FontSize(12)
                                    .Bold()
                                    .Append($"• {competency.Name}");

                    // Add the skill name paragraph
                    doc.InsertParagraph(pSkill);

                    // Add keywords if any
                    if (competency.Keywords != null && competency.Keywords.Any())
                    {
                        var keywordsText = string.Join(", ", competency.Keywords);
                        var pKeywords = doc.InsertParagraph()
                                           .Font("Arial")
                                           .FontSize(11)
                                           .Append(keywordsText);

                        // Add the keywords paragraph
                        doc.InsertParagraph(pKeywords);
                    }

                    isFirstSkill = false;
                }
            }
        }


        private void ReplaceText(string placeholder, string replacement, DocX doc, Paragraph paragraph)
        {
            if (paragraph.Text.Contains(placeholder))
            {
                paragraph.ReplaceText(new StringReplaceTextOptions()
                {
                    SearchValue = placeholder,
                    NewValue = replacement
                });
            }
        }


        private void ReplaceSkillsPlaceholder(DocX doc, string placeholder, List<Competency> competencies, Paragraph paragraph)
        {
            foreach (var competency in competencies.Take(2))
            {            
                var paragraphName = doc.InsertParagraph(competency.Name + ": ");
                paragraphName.Font("Albert Sans").FontSize(11).Bold(true);
                paragraph.InsertParagraphAfterSelf(paragraphName);

                var keywordsText = string.Join(", ", competency.Keywords);
                var paragraphKeywords = doc.InsertParagraph(keywordsText);
                paragraphKeywords.Font("Albert Sans").FontSize(10);
                paragraph.InsertParagraphAfterSelf(paragraphKeywords);
                
                paragraph.AppendLine();
            }
            
            doc.RemoveParagraph(paragraph);            
        }


        private void ReplacePlaceholderText(DocX doc, string placeholder, string replacement)
        {
            foreach (var paragraph in doc.Paragraphs)
            {
                paragraph.ReplaceText(new StringReplaceTextOptions()
                {
                    SearchValue = placeholder,
                    NewValue = replacement
                });
            }
        }


        private void NewPage(DocX doc)
        {
            doc.InsertSectionPageBreak();
            var section = doc.Sections.Last();

            // Add empty headers to this section
            section.AddHeaders();
            section.DifferentFirstPage = true;

            // Just to be absolutely safe, clear any paragraphs in headers
            section.Headers.First.Paragraphs.ToList().ForEach(p => p.Remove(false));
            section.Headers.Odd.Paragraphs.ToList().ForEach(p => p.Remove(false));
            section.Headers.Even.Paragraphs.ToList().ForEach(p => p.Remove(false));
        }
    }
}
