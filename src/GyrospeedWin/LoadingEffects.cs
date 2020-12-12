namespace GyrospeedWin {
    // Loading effects can be upto 0x1c bytes in length
    // They can freely use $fc, $fd or the X register (all of which are zeroed at start-up
    // and remain persistent between calls) and the accumulator (but not Y)

    // Entropy can come from:
    // ($c1),y which points to last byte read
    // $bd which stores the gradually increasing bits of the byte currently being read

    // But remember, they must be quick to execute (i.e. no delays) as they are called every time a bit is read
    static class LoadingEffects {
        public static readonly byte[][] Styles = new byte[][] {

            // Original style
            //      inc $d020
            //      rts

            new byte[] {
                0xEE, 0x20, 0xD0, 0x60
            },

            // Original style with double height Lines
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      inc $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x03, 0xEE, 0x20, 0xD0, 0x60
            },

            // FreeLoad
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      lda $fd
            //      cmp $c2
            //      lda $d020
            //      bcs xor
            //      adc #$01
            //      inc $fd
            //      inc $fd
            // xor  eor #$05
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x14, 0xA5, 0xFD, 0xC5, 0xC2, 0xAD, 0x20,
                0xD0, 0xB0, 0x06, 0x69, 0x01, 0xE6, 0xFD, 0xE6, 0xFD, 0x49, 0x05, 0x8D,
                0x20, 0xD0, 0x60
            },

            // FreeLoad Alternative
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      lda $fd
            //      cmp $c2
            //      lda $d020
            //      bcs xor
            //      adc #$01
            //      inc $fd
            //      inc $fd
            // xor  eor #$08 ; Just a colour difference from the standard FreeLoad style
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x14, 0xA5, 0xFD, 0xC5, 0xC2, 0xAD, 0x20,
                0xD0, 0xB0, 0x06, 0x69, 0x01, 0xE6, 0xFD, 0xE6, 0xFD, 0x49, 0x08, 0x8D,
                0x20, 0xD0, 0x60
            },

            // Stripe Columns
            //      inc $fd
            //      lda $fd
            //      sta $d020
            //      lda #$00
            //      sta $d020
            //      rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0x8D, 0x20, 0xD0, 0xA9, 0x00, 0x8D, 0x20, 0xD0,
                0x60
            },

            // Medium Stripes
            //      inc $fd
            //      lda $fd
            //      cmp #$04
            //      bcc skip
            //      inc $d020
            //      lda #$00
            //      sta $fd
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x07, 0xEE, 0x20, 0xD0, 0xA9,
                0x00, 0x85, 0xFD, 0x60
            },

            // Thick Stripes (US Gold Style)
            //      inc $fd
            //      lda $fd
            //      cmp #$0b ; Causes slightly thicker lines than the medium routine above
            //      bcc skip
            //      inc $d020
            //      lda #$00
            //      sta $fd
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x0B, 0x90, 0x07, 0xEE, 0x20, 0xD0, 0xA9,
                0x00, 0x85, 0xFD, 0x60
            },

            // Black and White
            //      inc $fd
            //      lda $fd
            //      cmp #$04
            //      bcc skip
            //      lda $fe
            //      eor #$01
            //      sta $d020
            //      sta $fd
            //      sta $fe
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0B, 0xA5, 0xFE, 0x49, 0x01,
                0x8D, 0x20, 0xD0, 0x85, 0xFD, 0x85, 0xFE, 0x60
            },

            // Jolly Stripes
            //      inc $fd
            //      lda $fd
            //      cmp #$04
            //      bcc skip
            //      lda $d020
            //      adc #$01
            //      eor #$05
            //      sta $d020
            //      lda #$00
            //      sta $fd
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0E, 0xAD, 0x20, 0xD0, 0x69,
                0x01, 0x49, 0x05, 0x8D, 0x20, 0xD0, 0xA9, 0x00, 0x85, 0xFD, 0x60
            },

            // Mixed-Up (Rack It Style)
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      lda $bd     ; Uses the data being read to add
            //      eor($c1),y  ; variance to the colour used
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x07, 0xA5, 0xBD, 0x51, 0xC1, 0x8D, 0x20,
                0xD0, 0x60
            },

            // Hi-Tec loader style thin stripe columns
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      dec $d020
            //      inc $d020
            // skip rts

            new byte[] {
               0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x06, 0xCE, 0x20, 0xD0, 0xEE, 0x20, 0xD0,
               0x60
            },

            // Black and red stripes
            //      inc $fd
            //      lda $fd
            //      cmp #$04
            //      bcc skip
            //      lda $d020
            //      adc #$02
            //      eor #$05
            //      sta $d020
            //      lda #$00
            //      sta $fd
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0B, 0xA5, 0xFE, 0x49, 0x02,
                0x8D, 0x20, 0xD0, 0x85, 0xFD, 0x85, 0xFE, 0x60
            },

            // Flashing and farting noise!
            //      inc $fd
            //      lda $fd
            //      cmp #$03
            //      bcc skip
            //      inc $d020
            //      inc $d418
            //      lda #$00
            //      sta $fd
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x03, 0x90, 0x0A, 0xEE, 0x20, 0xD0, 0xEE,
                0x18, 0xD4, 0xA9, 0x00, 0x85, 0xFD, 0x60
            },

            // Titus/Genias style thin light blue stripes on black border
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      lda #$0e ; Light blue thin stripes
            //      sta $d020
            //      lda #$00 ; Plain black screen
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x0A, 0xA9, 0x0E, 0x8D, 0x20, 0xD0, 0xA9,
                0x00, 0x8D, 0x20, 0xD0, 0x60
            },

            // Cruncher AB V1.0 Depack FX style (AND #$05)
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      inc $fd
            //      lda $fd
            //      and #$05
            //      sta $d020
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0D, 0xE6, 0xFE, 0xA5, 0xFE,
                0x29, 0x05, 0x8D, 0x20, 0xD0, 0xA9, 0x00, 0x85, 0xFD, 0x60
            },

            // Gremlin Deflektor / alternative World Games loader
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      lda $fd
            //      cmp $c2
            //      lda $d020
            //      bcs xor
            //      adc #$01
            //      inc $fd
            //      inc $fd
            // xor  eor #$0f ; Just a colour difference from the standard FreeLoad style
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x14, 0xA5, 0xFD, 0xC5, 0xC2, 0xAD, 0x20,
                0xD0, 0xB0, 0x06, 0x69, 0x01, 0xE6, 0xFD, 0xE6, 0xFD, 0x49, 0x0F, 0x8D,
                0x20, 0xD0, 0x60
            },

            // Firebird Black and Blue
            //      inc $fd
            //      lda $fd
            //      cmp #$0f
            //      bcc skip
            //      lda $fe
            //      eor #$06
            //      sta $d020
            //      sta $fd
            //      sta $fe
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x0F, 0x90, 0x0B, 0xA5, 0xFE, 0x49, 0x06,
                0x8D, 0x20, 0xD0, 0x85, 0xFD, 0x85, 0xFE, 0x60
            },

            // Two Shades of Grey with noise like a bumble bee!
            //      txa
            //      eor #$01
            //      tax
            //      beq skip
            //      inc $fd
            //      lda $fd
            //      ora #$0b
            //      sta $d020
            //      sta $d418
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x0C, 0xE6, 0xFD, 0xA5, 0xFD, 0x09, 0x0B,
                0x8D, 0x20, 0xD0, 0x8D, 0x18, 0xD4, 0x60
            },

            // Black and White Stripe Columns
            //      txa
            //      eor #$01
            //      bcs skip
            //      tax
            //      lda #$01
            //      sta $d020
            //      lda #$00
            //      sta $d020
            // skip rts

            new byte[] {
                0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x0A, 0xA9, 0x01, 0x8D, 0x20, 0xD0, 0xA9,
                0x00, 0x8D, 0x20, 0xD0, 0x60
            },

            // FINAL EFFECT ... A REAL DARK MYSTERY - AND A SIN :p
            //      inc $fd
            //      lda $fd
            //      cmp #$01
            //      bcc $118
            //      lda $d020
            //      eor #$09
            //      sta $d020
            //      lda $d418
            //      eor #$0f
            //      sta $d418
            // skip rts

            new byte[] {
                0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x01, 0x90, 0x10, 0xAD, 0x20, 0xD0, 0x49,
                0x09, 0x8D, 0x20, 0xD0, 0xAD, 0x18, 0xD4, 0x49, 0x0F, 0x8D, 0x18, 0xD4,
                0x60
            }
        };
    }
}
