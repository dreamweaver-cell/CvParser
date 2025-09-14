# How to set up OpenAI integration
Put your OpenAI API key in the appsettings.json file located in the CvParser.Web folder. 

 "ApiKey": "sk-your-openai-api-key-here"


# CvDocumentGenerator Service
Detta dokument ger en översikt av CvDocumentGenerator-tjänsten, dess huvudsyfte och föreslagna förbättringar baserade på en kodanalys.

## Syfte
CvDocumentGenerator är en C#-tjänst designad för att dynamiskt generera ett komplett Word-dokument (.docx) från ett Cv-dataobjekt. Den använder OpenXML SDK för att fylla en fördefinierad mall med personlig information, arbetslivserfarenheter, kompetenser och andra detaljer.

Tjänsten hanterar följande huvudfunktionalitet:

Mallval: Väljer automatiskt rätt mall baserat på språk (t.ex. svenska eller engelska).

Textutbyte: Ersätter textplatshållare i mallen (som |name| och |workexperiences|) med data från CV-objektet.

Bildhantering: Hanterar infogning av profilbilder, med en smart funktion som antingen byter ut bilddata i en befintlig platshållare (och bevarar formatet) eller infogar en ny bild om ingen platshållare hittas.

## Förklaring av flödet
Inmatning och validering: Processen börjar när en användare laddar upp en CV-fil. Systemet validerar filformatet för att säkerställa att det är en typ som kan behandlas, som exempelvis PDF eller DOCX.

Textutvinning: Därefter extraheras all rå text från dokumentet. För vissa filtyper eller skannade CV:n kan detta kräva optisk teckenigenkänning (OCR).

Parsing och strukturering: Detta är den mest kritiska delen av processen. Den utvunna råtexten analyseras för att identifiera olika sektioner (som arbetslivserfarenhet, utbildning och färdigheter) och extrahera specifika entiteter som namn, datum, företag och titlar. Datat rensas och normaliseras för att vara konsekvent.

Strukturerad utdata: Slutligen konverteras den strukturerade informationen till ett standardformat, oftast ett JSON-objekt, som sedan kan användas av andra system, till exempel en databas eller ett rekryteringsverktyg.
