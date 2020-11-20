namespace GyrospeedWin {
    // Loading effects can be upto 0x1c bytes in length
    // They can freely use $fc, $fd or the X register (all of which are zeroed at start-up
    // and remain persitent between calls) and the accumulator (but not Y)

    // Entropy can come from:
    // ($c1),y which points to last byte read
    // $bd which stores the gradually increasing bits of the byte currently being read

    // But remember, they must be quick to execute (i.e. no delays) as they are called every time a bit is read
    static class LoadingEffects {

        //      txa
        //      eor #$01
        //      tax
        //      beq skip
        //      inc $d020
        // skip rts

        public static readonly byte[] OriginalDoubleHeightLines = {
            0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x03, 0xEE, 0x20, 0xD0, 0x60
        };

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

        public static readonly byte[] FreeLoad = {
            0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x14, 0xA5, 0xFD, 0xC5, 0xC2, 0xAD, 0x20,
            0xD0, 0xB0, 0x06, 0x69, 0x01, 0xE6, 0xFD, 0xE6, 0xFD, 0x49, 0x05, 0x8D,
            0x20, 0xD0, 0x60
        };

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

        public static readonly byte[] FreeLoadAlternative = {
            0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x14, 0xA5, 0xFD, 0xC5, 0xC2, 0xAD, 0x20,
            0xD0, 0xB0, 0x06, 0x69, 0x01, 0xE6, 0xFD, 0xE6, 0xFD, 0x49, 0x08, 0x8D,
            0x20, 0xD0, 0x60
        };

        //      inc $fd
        //      lda $fd
        //      sta $d020
        //      lda #$00
        //      sta $d020
        //      rts

        public static readonly byte[] StripeColumns = {
            0xE6, 0xFD, 0xA5, 0xFD, 0x8D, 0x20, 0xD0, 0xA9, 0x00, 0x8D, 0x20, 0xD0,
            0x60
        };

        //      inc $fd
        //      lda $fd
        //      cmp #$04
        //      bcc skip
        //      inc $d020
        //      lda #$00
        //      sta $fd
        // skip rts

        public static readonly byte[] MediumStripes = {
            0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x07, 0xEE, 0x20, 0xD0, 0xA9,
            0x00, 0x85, 0xFD, 0x60
        };

        //      inc $fd
        //      lda $fd
        //      cmp #$0b ; Causes slightly thicker lines than the medium routine above
        //      bcc skip
        //      inc $d020
        //      lda #$00
        //      sta $fd
        // skip rts

        public static readonly byte[] ThickStripes = {
            0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x0B, 0x90, 0x07, 0xEE, 0x20, 0xD0, 0xA9,
            0x00, 0x85, 0xFD, 0x60
        };

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

        public static readonly byte[] BlackAndWhite = {
            0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0B, 0xA5, 0xFE, 0x49, 0x01,
            0x8D, 0x20, 0xD0, 0x85, 0xFD, 0x85, 0xFE, 0x60
        };

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

        public static readonly byte[] JollyStripes = {
            0xE6, 0xFD, 0xA5, 0xFD, 0xC9, 0x04, 0x90, 0x0E, 0xAD, 0x20, 0xD0, 0x69,
            0x01, 0x49, 0x05, 0x8D, 0x20, 0xD0, 0xA9, 0x00, 0x85, 0xFD, 0x60
        };
 
        //      txa
        //      eor #$01
        //      tax
        //      beq skip
        //      lda $bd     ; Uses the data being read to add
        //      eor($c1),y  ; variance to the colour used
        //      sta $d020
        // skip rts

        public static readonly byte[] MixedUp = {
            0x8A, 0x49, 0x01, 0xAA, 0xF0, 0x07, 0xA5, 0xBD, 0x51, 0xC1, 0x8D, 0x20,
            0xD0, 0x60
        };
    }
}
