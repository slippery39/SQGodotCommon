extends SceneTree

# Renders authored card art to PNG so it can be LOOKED AT — at 256, and at 40, which is the
# lane-figure size KinUI.md says a subject has to survive.
#
# **Kept, because you cannot tell what an SVG looks like by reading it.** A cleaver drawn with a
# curved blade rendered as a frying pan and the markup said nothing. See the `draw-card-art` skill
# and Commands.md; it loads the IMPORTED resource, so --import has to run after every edit.

func _initialize():
	var out = OS.get_environment("ART_OUT")
	var names = OS.get_environment("ART_NAMES").split(",")
	for name in names:
		var tex = load("res://KinGame/Art/%s.svg" % name)
		if tex == null:
			print("MISSING ", name)
			continue
		var img: Image = tex.get_image()
		img.resize(256, 256, Image.INTERPOLATE_LANCZOS)
		# On a navy board, not on transparency — a silhouette reads differently over nothing.
		var plate := Image.create(256, 256, false, img.get_format())
		plate.fill(Color("#16212E"))
		plate.blend_rect(img, Rect2i(0, 0, 256, 256), Vector2i(0, 0))
		plate.save_png("%s/%s_256.png" % [out, name])
		var small := plate.duplicate()
		small.resize(40, 40, Image.INTERPOLATE_LANCZOS)
		small.resize(160, 160, Image.INTERPOLATE_NEAREST)
		small.save_png("%s/%s_40.png" % [out, name])
		print("rendered ", name)
	quit()
