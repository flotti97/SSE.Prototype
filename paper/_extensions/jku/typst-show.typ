#show: coverpage.with( //What is what, see jku.typ
  title: $if(title)$ [$title$] $else$ "<Your Title>" $endif$,
  submitters: 
    $if(submitters)$ 
      ("$submitters.name$$if(submitters.matriculation-number)$ $submitters.matriculation-number$ $endif$",) 
    $else$ 
      ("<Your name>",) 
    $endif$,
  department: $if(department)$ [$department$] $else$ "<JKU Department>" $endif$,
  supervisors: 
  $if(supervisors)$ 
      ($for(supervisors)$"$supervisors$"$sep$, $endfor$,) 
    $else$ 
      ("<Supervisor name>",) 
  $endif$,
  date: datetime.today().display("[month repr:long] [year]"),
  typeOfWork: $type-of-work$,
  state: 0,
  version: $version$,
  degree: "$degree$",
  study: "$study$"
)

#show "OTMEvolver": text(font: font.sans, hyphenate: false)[OTM#super([Evolver])]

#show: preface.with(
  statutoryDeclaration: false,
  // If you do not need abstract, zusammenfassung or ack, just comment them out or remove line
  abstract: "$abstract$",
  zusammenfassung: "$zusammenfassung$",
  acknowledgement: "$acknowledgement$",
  tocDepth: 3, //Depth of headings shown in ToC; e.g. 1 = Only h1 shown, 3 = H1 to H3 shown
  tableOfFigures: true, //Set to false if any tableOf is not needed
  tableOfTables: true,
  tableOfCode: true,
)

$for(abbrevs)$
#abbreviate("$abbrevs.key$", "$abbrevs.value$")
$endfor$

#show: mainContent.with(
  //heading-depth: 3 
)

#show link: set text(fill: blue, hyphenate: true)
#show cite: it => text(fill: black, style: "normal", it)
#show ref: it => text(weight: "medium", style: "italic", it)

// #heading(supplement: "Chapter")[Content] <chap-content-label>
// #include "content/01-introduction.typ"
// #pagebreak()


// #counter(heading.where(level: 1)).update(it => 0) // workaround for header to not show a number for bib
// #show bibliography: set text(size: 8pt)
// #show link: it => box(clip: true, it)
// #bibliography("bibliography.bib", style: "ieee") //Select your bib style here

// #show_endnotes() //Comment this out if you have no endnotes

#counter(heading).update(0)
#pagebreak(weak: true)

// #set heading(numbering: "A.1.")
// #set page(flipped: false) //To go in landscape mode, set to true if needed for attachments
// #heading("Attachments") <attachments>
// #include "content/attachment-a.typ"
