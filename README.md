# How to set up OpenAI integration
Put your OpenAI API key in the appsettings.json file located in the CvParser.Web folder. 

 "ApiKey": "sk-your-openai-api-key-here"


# CvParser

This document provides an overview of the **CvParser** service.

## Purpose

CvParser is a .NET service designed to dynamically generate a complete Word document (.docx) from a CV data object.
It uses the **OpenXML SDK** to fill a predefined template with personal information, work experiences, skills, and other details.

The service provides the following main functionality:

* **Template selection** – Automatically chooses the correct template based on language (Swedish or English).
* **Text replacement** – Replaces text placeholders in the template (such as `|name|` and `|workexperiences|`) with data from the CV object.
* **Image handling** – Inserts profile images, with the ability to replace image data inside an existing picture while preserving its formatting.

## Flow Explanation

* **Input and validation** – The process begins when a user uploads a CV file.
  The system validates the file format to ensure it is supported, such as PDF or DOCX.

* **Text extraction** – All raw text is extracted from the uploaded document.

* **Parsing and structuring** – The extracted raw text is analyzed to identify different sections
  (such as work experience, education, and skills) and to extract specific entities like names, dates, companies, and job titles.
  The data is cleaned and normalized for consistency.

* **Structured output** – Finally, the structured information is converted into a standard format (JSON object),
  which is then used to generate the final **Xamera CV**.

