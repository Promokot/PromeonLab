// Which LOCAL axis a terminal (leaf) proxy bone points along. Auto = legacy behavior
// (orient along the direction from the parent bone) and the back-compat default for any
// rig without an explicit choice. X/Y/Z are the bone's positive local axes.
public enum TerminalBoneAxis
{
    Auto = 0,
    X    = 1,
    Y    = 2,
    Z    = 3,
}
