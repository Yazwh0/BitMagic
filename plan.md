# Plan: file views and library sections

## Status

Implemented, including BitBench (step 8). Where the build differs from the plan below:

- **Nested imports.** Every library initialises what it imports, once per build, imports first. For a while only `file` libraries and views were, because BitBench registered `window_dragging.bmasm`'s code by hand from `window.bmasm` as well as in `window_dragging.bmasm`'s own `Initialise()`; converting BitBench removed that, and the rule is now universal. Any library that registers an imported library's code itself will now write it twice: there's no changelog to note this in, so it's on the Libraries docs page.
- **`library.bmasm`** is `library BitMagic.Views.Library;`, so its assembly is `BitMagic.Views` rather than a bare `BitMagic`.
- **API additions:** `CapturedAnchor` (an anchor placed while capturing, so `place` works inside a `file library`), `TemplateSourceException` (the base of `ViewException`; section errors use it too, and the engine reports it as a `TemplateCompilationException` at the file and line), `Section.IsValidName`, and `LibraryBase.Instance<T>()` / `InitialiseLibrary` / `InitialiseImports` / `SetOwners` (generated code and the engine use these; hidden from IntelliSense).
- **`Template.Keyword`** takes the line and file too: `Template.Keyword(keyword, arguments, line, file)`.
- **The `;` on an import is now optional**, so `import Dos="dos.bmasm"` works as the examples below show.
- **Checks:** before converting BitBench, its generated asm and PRG were byte-identical with the old engine. After converting it, the generated asm has exactly the same lines, but `window_dragging.bmasm`'s come before `window.bmasm`'s, as a dependency now initialises first; its `.align 32` then needs 5 fewer bytes of padding, so the PRG is 5 bytes smaller. The Visibility example, split into `main.bmasm` and a `file library code` `text.bmasm`, builds through X16D to the same PRG as the single-file version. Tests: `BitMagic.X16Emulator.Tests/Template/Sections.cs`.
- **Not checked:** breakpoints and stepping in a library file in the debugger, which needs the new X16D in VS Code.

## Goal

Let a library be plain bmasm, with no C# boilerplate, and replace hand-written plumbing like BitBench's `initialisation.bmasm`. Do it with a framework users can extend: the built-in behaviour is written as an ordinary bmasm library on the same public API anyone else can use.

Three pieces:

1. **`file <kind>`**: a header line that hands the file to a **view**, an ordinary bmasm library called `<kind>.bmasm`. This is the only new rule in the compiler, and it knows nothing about what a view does.
2. **The section API**: `Section`, `Build`, `Template.Anchor` and friends, in `BitMagic.TemplateEngine.Objects`.
3. **`library.bmasm`**: the view behind `file library`. It splits a file into sections and holds the default placement. It ships next to `BM.bmasm` in `BitMagic.TemplateEngine.Compiler/Library`.

## Today

A BitBench library:

```bmasm
library BitBench.Memory.Manager;
import Init="initialisation.bmasm";

private static void Setup()
{
    .segment bench_memory, scope=bench_memory
    .padvar byte[256] blocks
    .endsegment
}

private static void Proc()
{
    .scope bench_memory
    .proc initialise
    ...
    .endscope
}

public override void Initialise()
{
    Init.Add(Proc);
    Setup();
}
```

`main.bmasm` imports `Init` as well and calls `Init.Setup();` where the procs should go. `initialisation.bmasm` is a hand-written section registry with two sections: one written immediately (`Setup`), one deferred (`Proc`).

## Part 1: what the user writes

### A library

```bmasm
; text.bmasm
file library

section setup
    .segment zp, scope=text
    .padvar byte lines
    .endsegment

section code
.scope private text
    .proc public print
        ...
        inc lines
        rts
    .endproc
.endscope
```

`section` has no `.`: like `import`, `library` and `file`, it's handled above the assembler. Template code (`@(...)`, `for` loops) still works anywhere in the file.

Every line of asm must be in a section. Blank lines, comments and header lines (`import`, `using`, `nuget`, ...) can come before the first `section`; anything else there is an error:

```text
text.bmasm(3): 'lda #1' is before the first 'section'. Every line in a 'file library' file must be in a section, or name a starting section: 'file library code'.
```

A library with only one section can name it on the `file` line instead. Everything goes into that section until a `section` line changes it:

```bmasm
; text.bmasm
file library code

.scope private text
    .proc public print
    ...
```

The compiler doesn't interpret anything after `file <kind>`: it's passed to the view as `ViewFile.Arguments`, and each view decides what it means. For `library` it's the starting section.

### main, using the defaults

```bmasm
import BM="bm.bmasm"
import Text="text.bmasm"

    BM.X16Header();

    ldx #<hello
    ldy #>hello
    jsr text:print
    ...
```

The program comes out as:

```text
every library's setup
main
every library's code
```

`main` needs nothing new. `setup` is written before `BM.X16Header()`, so it is for declarations (segments, variables, constants) only; code there would land ahead of the BASIC stub. This matches how BitBench's `Setup()` is used today.

### main, placing sections itself

```bmasm
import BM="bm.bmasm"
import Text="text.bmasm"

    BM.X16Header();
    place setup                     ; after the BASIC stub

    jsr text:print
    ...

    .segment high_code
    place code                      ; every library's procs, in this segment
    .endsegment
```

`place <name>` is a template-level keyword, like `import`: no `.`, and it works in any file, not only `main`. It's generic: it places whatever section has that name, built in or your own. A section placed explicitly isn't placed again by the defaults. Writing `place code` twice is an error.

### Placing one library's code

Different libraries' code often belongs in different places: DOS code in RAM bank 1, decompression code in the main program. `place <alias>.<section>` places just one library's part of a section:

```bmasm
import BM="bm.bmasm"
import Dos="dos.bmasm"
import Lz="decompress.bmasm"

    BM.X16Header();
    ...

    .segment dos_bank               ; RAM bank 1
    place Dos.code                  ; only dos.bmasm's code
    .endsegment

    place code                      ; every other library's code, Lz's included
```

- The qualifier is the **import alias**, so `Dos` means the same as in `Dos.Something()`. It's resolved using the imports of the file the `place` line is in.
- To place a library you don't use directly (one `dos.bmasm` imports, say), import it here too to give it an alias. Imports are deduplicated, so this costs nothing.
- `place code` places every part of `code` that isn't placed anywhere else. Order doesn't matter: `place code` above `place Dos.code` still leaves Dos's code out of it, because placements are resolved at the end of the build.
- The defaults work the same way: if nothing writes `place code`, whatever is left goes at the end. Qualifying one library never means placing all the others by hand.
- Section names can't contain `.`, so `Dos.code` is never ambiguous.

Every part is tracked, so nothing is written twice and nothing is silently dropped. These are errors:

```text
main.bmasm(30): 'dos.bmasm' code is already placed at main.bmasm(12).
main.bmasm(12): 'Dso' isn't an import in this file.
main.bmasm(12): 'dos.bmasm' has no 'cod' section.
main.bmasm(40): 'place code' is already used at main.bmasm(31).
Section 'irq' has code from 'music.bmasm' that is never placed. Add 'place irq' or 'place Music.irq'.
```

### Your own section

Any name works. Only `setup` and `code` have defaults; anything else must be placed.

```bmasm
; music.bmasm
file library

section irq
    jsr music:tick

section code
.scope music
    .proc tick
    ...
```

```bmasm
; main
.proc irq_handler
    place irq                       ; jsr music:tick, and any other library's irq code
    jmp (old_irq)
.endproc
```

Not placing it fails the build:

```text
Section 'irq' has code from 'music.bmasm' that is never placed. Add 'place irq' or 'place Music.irq'.
```

### Changing how a section is written

Every section has a `Writer`. The default writes each library's part in turn; any library can replace it.

```bmasm
; irqchain.bmasm
library MyGame.IrqChain;

private static void WriteIrq(IReadOnlyList<SectionPart> parts)
{
    .proc irq_handler
    foreach (var part in parts)
    {
        part.Write();
    }
    jmp (old_irq)
    .endproc
}

public override void Initialise()
{
    Section.Get("irq").Writer = WriteIrq;
}
```

`main` imports `irqchain.bmasm` and writes `place irq`; the whole handler is written there.

### Your own view

`file <kind>` uses `<kind>.bmasm`, found like an import: next to the file, then in the install `Library` folder. A view is a normal `library` file with a `Process` method, and it can claim its own keywords.

```bmasm
; data.bmasm: the view behind 'file data'
library MyGame.DataView;

public static IEnumerable<string> Keywords => ["bank"];

public static void Process(ViewFile file)
{
    var bank = int.Parse(file.Arguments ?? "0");    // 'file data 3' starts in bank 3
    var lines = new List<CapturedLine>();

    foreach (var item in file.Capture())
    {
        if (item is KeywordLine { Keyword: "bank" } k)
        {
            bank = int.Parse(k.Arguments);
            continue;
        }

        lines.Add((CapturedLine)item);
    }

    Section.Get("data").Add(file.Owner, () =>
    {
        .segment @($"data_bank_{bank}")
        Template.Write(lines);
        .endsegment
    });
}
```

```bmasm
; levels.bmasm
file data
bank 3

.level1:
    .byte 1, 2, 3
.level2:
    .byte 4, 5, 6
```

### C# libraries

A library that also has C# helpers (like BitBench's `window.bmasm`) keeps the `library X;` form and uses the API directly. `section` lines are only for `file` files.

```bmasm
library BitBench.Window;

private static void Setup() { ... }
private static void Proc()  { ... }

public static void OutputWindowLine() { ... }

public override void Initialise()
{
    Section.Get("setup").Add(Owner, Setup);
    Section.Get("code").Add(Owner, Proc);
}
```

## Part 2: how it works

### The `file <kind>` rule (compiler)

In `MacroAssembler`, before `CreateTemplate`:

1. If the first line that isn't blank or a comment is `file <kind> [arguments]` (optional `;`), the file is a view file. The arguments are the rest of the line, trimmed, or `null` if there are none. Only that line is checked, so C#'s `file class X` modifier is never mistaken for it.
2. `<kind>.bmasm` is found and built exactly like an import. If it isn't found, the error is `ImportNotFoundException`, listing the paths searched.
3. The compiler reads the view's `Keywords` (if it has any) from the built assembly, and checks it has `public static void Process(ViewFile file)`. If not: `'data.bmasm' can't be used with 'file data': it has no 'public static void Process(ViewFile file)'.`
4. The file is rewritten one output line per input line, so source maps and error lines don't move:

   | `text.bmasm` | generated |
   | --- | --- |
   | `file library` | `private static void __body() {` |
   | `import BM="bm.bmasm"` | *(handled as now)* |
   | `section setup` | `Template.Keyword("section", "setup");` |
   | `    .padvar byte lines` | `    .padvar byte lines` *(as now)* |
   | *(end of file)* | `}` |

   A line is a keyword line if its first word is one of the view's `Keywords`.

5. It appends:

   ```csharp
   public override void Initialise()
   {
       BitMagic.Library.Process(new ViewFile(Owner, null, __body)     // 'file library code' would pass "code");
   }
   ```

6. A `file` file listed in `project.json` as the program, rather than imported, is an error.

### The `place <name>` rule (compiler)

In `CreateTemplate`, for any file, on the same line:

| line | becomes |
| --- | --- |
| `place code` | `Section.Get("code").Place("main.bmasm", 31);` |
| `place Dos.code` | `Section.Get("code").Place("main.bmasm", 12, owner: "<full path of dos.bmasm>");` |

The alias is resolved at compile time from the file's own imports (`ImportToFilename`), so an unknown alias is a compile error on that line. The file and line are passed so later errors can point at the `place` line. Otherwise the rule is generic: the compiler doesn't know which sections exist, and an unknown section is caught by the end-of-build checks.

### `library.bmasm`

```bmasm
library BitMagic.Library;

public static IEnumerable<string> Keywords => ["section"];

// file library: split the file's output into sections
public static void Process(ViewFile file)
{
    var sections = new Dictionary<string, List<CapturedLine>>();
    string current = file.Arguments;                // 'file library code' starts in code

    foreach (var item in file.Capture())
    {
        if (item is KeywordLine { Keyword: "section" } k)
        {
            current = k.Arguments;
            continue;
        }

        if (current == null)
            throw new ViewException(item, $"'{((CapturedLine)item).Text.Trim()}' is before the first 'section'. Every line in a 'file library' file must be in a section, or name a starting section: 'file library code'.");

        if (!sections.TryGetValue(current, out var lines))
            sections[current] = lines = new();

        lines.Add((CapturedLine)item);
    }

    foreach (var (name, lines) in sections)
        Section.Get(name).Add(file.Owner, () => Template.Write(lines));
}

// default placement for anything not placed explicitly
public override void Initialise()
{
    Anchor start = null;

    Build.OnStart(() => start = Template.Anchor());

    Build.OnEnd(() =>
    {
        // whatever nobody placed: setup at the start, code at the end
        if (!Section.Get("setup").RestPlaced)
            Section.Get("setup").PlaceAt(start);

        if (!Section.Get("code").RestPlaced)
            Section.Get("code").Place();
    });
}
```

Every `file library` file imports `library.bmasm` (step 2 above), so the defaults always apply when a library is used.

### The API (`BitMagic.TemplateEngine.Objects`)

```csharp
// a spot in the output, filled in when the build ends
public sealed class Anchor { }

// what Capture returns: output lines, and keyword markers for the view
public abstract record class CapturedItem(int Line, string SourceFile);
public sealed record class CapturedLine(string Text, int Line, string SourceFile) : CapturedItem(Line, SourceFile);
public sealed record class KeywordLine(string Keyword, string Arguments, int Line, string SourceFile) : CapturedItem(Line, SourceFile);

public static class Template
{
    public static Anchor Anchor();                                   // mark the current position
    public static IDisposable WriteTo(Anchor anchor);                // output goes there until disposed
    public static IReadOnlyList<CapturedItem> Capture(Action body);  // run body, collect what it writes
    public static void Write(IEnumerable<CapturedLine> lines);       // write lines, keeping their source map
    public static void Keyword(string keyword, string arguments);    // a marker, only valid inside Capture
    // ...existing WriteLiteral etc
}

// an error at a line of the file being viewed; reported with that file and line
public sealed class ViewException(CapturedItem at, string message) : Exception(message);

public static class Build
{
    public static void OnStart(Action action);   // after every Initialise(), before main
    public static void OnEnd(Action action);     // after main
}

public sealed class ViewFile
{
    public ViewFile(string owner, string arguments, Action body);
    public string Owner { get; }                    // the viewed file's source path
    public string Arguments { get; }                // after 'file <kind>', or null
    public IReadOnlyList<CapturedItem> Capture();   // runs the body once, then returns the same result
}

public sealed class SectionPart
{
    public string Owner { get; }
    public Action Write { get; }
    public bool Placed { get; }                      // set when the build resolves placements
}

public sealed class Section
{
    public static Section Get(string name);          // created on first use
    public static IEnumerable<Section> All { get; }

    public string Name { get; }
    public IReadOnlyList<SectionPart> Parts { get; }
    public bool RestPlaced { get; }                  // an unqualified placement exists

    public void Add(string owner, Action write);     // the same owner twice is ignored

    // the rest: every part not placed by owner. A second one is an error.
    public void Place(string file = null, int line = 0);
    public void PlaceAt(Anchor anchor, string file = null, int line = 0);

    // one library's part. Placing the same owner twice is an error.
    public void Place(string file, int line, string owner);
    public void PlaceAt(Anchor anchor, string file, int line, string owner);

    // how parts are written: called once per placement, with the parts placed there.
    // The default writes each in turn.
    public Action<IReadOnlyList<SectionPart>> Writer { get; set; }
}

public abstract class LibraryBase
{
    public string Owner { get; }                     // the library's source file, set by the engine
    public virtual void Initialise() { }
}
```

### Why anchors

Placing a section reserves a spot; every section is written into its spot at the end of the build. That allows:

- **The default `setup` position.** `library.bmasm` reserves a spot at the start before anyone knows whether `main` will place `setup` further down, and only uses it if `main` didn't.
- **Order-free placement.** `place code` means "everything not placed elsewhere", which can only be known once every `place Lib.code` has been seen.
- **Late additions.** C# helpers that `main` calls halfway through can still add to a section placed earlier. BitBench's `Global.AddMouseHandler(name)` is this case.

### A build, step by step

```text
1. Initialise() for every library, once each, imports before importers
     a 'file library' file -> library.bmasm Process() -> Section.Add(...)
     library.bmasm         -> registers its OnStart / OnEnd hooks
2. OnStart hooks          library.bmasm reserves the start anchor
3. main runs              each 'place x' / 'place Lib.x' reserves an anchor
4. OnEnd hooks            library.bmasm places setup / code if nobody did
5. Resolve and check      owner placements claim their parts; each section's rest
                          placement takes what's left. Errors for: a part placed
                          twice, an owner placement with no part to place, a
                          second rest placement, and any part still unplaced
                          no KeywordLine reached the final output
6. Write                  each section's Writer runs into its anchor
7. Generate               chunks and source maps are joined and passed to the compiler as now
```

## Part 3: implementation

| # | Where | Change |
| - | ----- | ------ |
| 1 | `TemplateEngine.Objects/Template.cs` | Output becomes a list of chunks, each with its own source map. Add `Anchor`, `WriteTo`, `Capture`, `Write` and `Keyword`. |
| 2 | `TemplateEngine.Objects` | New `Build`, `Section`, `SectionPart`, `ViewFile`, `CapturedItem`/`CapturedLine`/`KeywordLine`. Add `Owner` to `LibraryBase`. |
| 3 | `TemplateEngine.Objects/ITemplateRunner.cs` | `TemplateRunner.Run()` follows the build steps above. |
| 4 | `TemplateEngine.Compiler/MacroAssembler.cs` | The `file <kind>` and `place [alias.]<name>` rules. Set each library's `Owner` to its source file. Each library's `Initialise()` runs once per build (today `main` calls one per import, on a new instance), and imported libraries initialise their own imports (today only `main` does). |
| 5 | `TemplateEngine.Compiler/Library/library.bmasm` | New, as above, copied to the output like `BM.bmasm`. |
| 6 | `BitMagic.VSC` grammar | `file <kind>` on the first line, `section <name>` and `place [alias.]<name>`. |
| 7 | `BitMagic.X16Emulator.Tests` | See tests below. |
| 8 | `BitMagic.Examples/BitBench` | Delete `initialisation.bmasm`. Libraries without C# helpers become `file library`; the rest call `Section.Add` in `Initialise()`. Remove `Init` from `main.bmasm` and `test.bmasm`. Check the PRG is byte-identical. |
| 9 | `BitMagic.Examples/Visibility` | Move the `text` scope into `text.bmasm` with `file library`. |
| 10 | `BitMagic.Documentation` | A Template Engine page on views and sections, and the regenerated example pages. |

### Tests

- Default order: `setup`, then `main`, then `code`.
- `place setup` and `place code`, and the defaults then skip them.
- `place` in a library, not only `main`.
- Asm before the first `section` in a `file library` file: error with file and line.
- `file library code`: asm before any `section` goes into `code`, and a later `section` still switches.
- Arguments reach a custom view as `ViewFile.Arguments`.
- A custom section placed by `main`.
- A section with parts that is never placed: error naming the section and the files.
- `place code` twice: error.
- `place Dos.code` in a segment and `place code` elsewhere: Dos's code only in the segment, everything else at `place code`, in either order.
- `place Dos.code` with no `place code`: the rest still goes to the default position.
- `place Dos.code` twice: error pointing at both lines.
- `place Dso.code` (unknown alias): compile error on that line.
- `place Dos.cod` (no such part): error.
- `place` for a library imported only to give it an alias.
- A custom `Writer` with two placements is called once for each, with the right parts.
- A custom `Writer`.
- A custom view with its own keyword.
- A view with no `Process`: error.
- `file` with an unknown kind: import-not-found error.
- A `file` file used as the main program: error.
- A library imported by two files is written once.
- A library imported only by another library is still initialised and written.
- A late addition to a section placed earlier.
- An assembler error inside a section reports the library file and line.
- A C# `file class` line is not taken as a header.

The debugger builds through the same `MacroAssembler`, so breakpoints and stepping in a library should land on the library's own lines without changes. Check that by hand on the Visibility example.

## Decisions

1. **Lines before the first `section`** in a `file library` file are an error (`ViewException`, with the file and line). Blank lines, comments and header lines are allowed there.
2. **Placement** is the generic template-level keyword `place <name>`, usable in any file. No import is needed for it.
3. **Owner** is the library's source file: its full path, with `/` and `\` normalised so the same file always matches on Windows and Linux. Errors show it relative to the project (`bench/memory.bmasm`). The engine sets it when it creates the library rather than baking it into the cached assembly, so moving a project doesn't need a rebuild.
4. **Arguments on the `file` line** are passed to the view untouched. `library` uses them as the starting section, so `file library code` needs no `section` line, while plain `file library` keeps decision 1.
5. **Per-library placement** is `place <alias>.<section>`, where the alias is an import in the same file. Every part is tracked: a part is written exactly once, `place <section>` takes whatever isn't placed elsewhere (in any order), and anything still unplaced at the end of the build is an error.
