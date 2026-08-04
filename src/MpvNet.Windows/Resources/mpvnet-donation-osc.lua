-- Adds a small support button beside the standard OSC controls.
-- The built-in osc.lua is intentionally left untouched.

local overlay = mp.create_osd_overlay("ass-events")
overlay.z = 100

local button = {
    width = 24,
    height = 24,
    -- The standard bottombar places the volume and fullscreen buttons on
    -- this row. Keep the heart immediately before them without covering the
    -- track-selection indicator.
    right = 58,
    bottom = 0,
}

local visible = false
local binding_active = false
local last_mouse_x = nil
local last_mouse_y = nil
local last_mouse_move = 0
local auto_hide_timeout = 0.5
local heart = string.char(0xE2, 0x99, 0xA5)

local function get_dimensions()
    local dimensions = mp.get_property_native("osd-dimensions") or {}
    return dimensions.w or 0, dimensions.h or 0
end

local function get_mouse_position()
    local position = mp.get_property_native("mouse-pos") or {}
    return position.x or -1, position.y or -1
end

local function is_inside(x, y, width, height)
    local left = width - button.right - button.width
    local top = height - button.bottom - button.height
    return x >= left and x <= left + button.width and
        y >= top and y <= top + button.height
end

local function clear_overlay()
    overlay.data = ""
    overlay:update()
end

local function draw_button(width, height, hovered)
    local left = width - button.right - button.width
    local top = height - button.bottom - button.height
    local color = hovered and "&HFFFFFF&" or "&HE0E0E0&"
    local border = hovered and "&H606060&" or "&H202020&"

    overlay.res_x = width
    overlay.res_y = height
    overlay.data = string.format(
        "{\\an7\\pos(%d,%d)\\fs20\\fnSegoe UI Symbol\\1c%s\\3c%s\\bord1\\shad0}%s",
        left + 2, top + 1, color, border, heart)
    overlay:update()
end

local function open_donation_page()
    local url = mp.get_property("user-data/mpvnet-donation-url")
    if url and url ~= "" then
        mp.commandv("script-message-to", "mpvnet", "shell-execute", url)
    end
end

local function remove_binding()
    if binding_active then
        mp.remove_key_binding("mpvnet-donation-button")
        binding_active = false
    end
end

local function add_binding()
    if not binding_active then
        mp.add_forced_key_binding("MBTN_LEFT", "mpvnet-donation-button", open_donation_page)
        binding_active = true
    end
end

local function update()
    local width, height = get_dimensions()
    if width < 420 or height < 120 then
        visible = false
        remove_binding()
        clear_overlay()
        return
    end

    local mouse_x, mouse_y = get_mouse_position()
    local mouse_hover = mp.get_property_bool("mouse-pos/hover")
    local visibility_mode = mp.get_property("user-data/osc/visibility") or "auto"
    local hovered = is_inside(mouse_x, mouse_y, width, height)

    if mouse_hover and (mouse_x ~= last_mouse_x or mouse_y ~= last_mouse_y) then
        last_mouse_x = mouse_x
        last_mouse_y = mouse_y
        last_mouse_move = mp.get_time()
    end

    local should_show = visibility_mode == "always" or
        (visibility_mode == "auto" and mouse_hover and
            (hovered or mp.get_time() - last_mouse_move <= auto_hide_timeout))

    if visibility_mode == "never" or not should_show then
        visible = false
        remove_binding()
        clear_overlay()
        return
    end

    visible = true
    draw_button(width, height, hovered)

    -- Only intercept a click while the pointer is over this button. The OSC's
    -- own forced bindings remain untouched everywhere else.
    if hovered then
        add_binding()
    else
        remove_binding()
    end
end

mp.observe_property("osd-dimensions", "native", update)
mp.observe_property("mouse-pos", "native", update)
mp.observe_property("mouse-pos/hover", "bool", update)
mp.observe_property("user-data/osc/visibility", "string", update)
mp.add_periodic_timer(0.1, update)
update()
